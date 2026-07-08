using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace FileHistory
{
    /// <summary>
    /// 保持ポリシー（最大世代数・保持日数）に従って古いバックアップを
    /// 自動削除するバックグラウンドワーカー
    /// </summary>
    public class RetentionWorker : IDisposable
    {
        // DI
        readonly Settings _settings;
        readonly IBackupDb _db;
        readonly ILogger _logger;

        readonly CancellationTokenSource _cts;
        readonly Thread _thread;

        // 起動直後の負荷を避けるための初回待機時間
        static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

        public RetentionWorker(Settings settings, IBackupDb db, ILoggerFactory loggerFactory)
        {
            _settings = settings;
            _db = db;
            _logger = loggerFactory.CreateLogger<RetentionWorker>();
            _cts = new CancellationTokenSource();

            if (_settings.MaxGenerations <= 0 && _settings.RetentionDays <= 0)
            {
                _logger.LogInformation("Retention policy disabled (MaxGenerations/RetentionDays not set)");
                return;
            }

            _thread = new Thread(() => Run(_cts.Token))
            {
                Priority = ThreadPriority.Lowest,
                IsBackground = true,
            };
            _thread.Start();
        }

        void Run(CancellationToken token)
        {
            if (token.WaitHandle.WaitOne(StartupDelay)) return;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    ApplyRetentionPolicy(token);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception caught in RetentionWorker: {ex}", ex.ToString());
                }
                if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Max(60, _settings.RetentionScanInterval)))) return;
            }
        }

        /// <summary>
        /// 保持ポリシーを一回適用する
        /// 各ファイルの最新世代は常に保持し、それ以外について
        /// MaxGenerations超過分およびRetentionDaysより古い世代を削除する
        /// </summary>
        public void ApplyRetentionPolicy(CancellationToken token)
        {
            _logger.LogInformation("Retention scan start (MaxGenerations = {max}, RetentionDays = {days})",
                _settings.MaxGenerations, _settings.RetentionDays);
            var deleted = 0;
            var ageLimit = _settings.RetentionDays > 0 ? DateTime.Now.AddDays(-_settings.RetentionDays) : DateTime.MinValue;

            foreach (var file in _db.FindAllFiles())
            {
                if (token.IsCancellationRequested) break;

                var attrs = _db.GetAttributes(file.Id).OrderByDescending(m => m.BackupTime).ToList();
                if (attrs.Count <= 1) continue;
                var backupFileDir = _db.GetFileDir(file.Id);

                // i = 0（最新世代）は常に保持
                for (int i = 1; i < attrs.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var expiredByAge = _settings.RetentionDays > 0 && attrs[i].BackupTime < ageLimit;
                    var expiredByCount = _settings.MaxGenerations > 0 && i >= _settings.MaxGenerations;
                    if (!expiredByAge && !expiredByCount) continue;

                    var backupPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, file.Name), attrs[i].BackupTime);
                    try
                    {
                        _logger.LogInformation("Retention delete {path}", backupPath);
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        _db.DeleteAttribute(attrs[i].Id);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Exception caught in deleting {path}: {ex}", backupPath, ex.ToString());
                    }
                }
            }
            _logger.LogInformation("Retention scan finished, {count} backups deleted", deleted);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _thread?.Join();
        }
    }
}
