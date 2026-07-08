using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileHistory
{
    /// <summary>
    /// ファイルクローラ
    /// </summary>
    public class Crawler : IDisposable
    {
        // DI
        readonly Settings _settings;
        readonly IBackupDb _db;
        readonly ILogger _logger;
        readonly List<Thread> _threads;
        readonly List<AutoResetEvent> _triggerEvents;
        readonly CancellationTokenSource _cts;
        readonly IBackupScheduler _backupScheduler;

        // クローリング一時停止用のセマフォ
        public int CrawlingSuspended;

        // クローリング済みのファイル一覧
        readonly ConcurrentDictionary<string, AttributeFileEntry> _checkedFiles;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public Crawler(Settings settings, IBackupDb db, ILoggerFactory loggerFactory, IBackupScheduler backupScheduler)
        {
            _logger = loggerFactory.CreateLogger<Crawler>();
            _logger?.LogTrace("Enter: Crawler()");

            _settings = settings;
            _db = db;
            _checkedFiles = new ConcurrentDictionary<string, AttributeFileEntry>();
            _threads = new List<Thread>();
            _triggerEvents = new List<AutoResetEvent>();
            _cts = new CancellationTokenSource();
            _backupScheduler = backupScheduler;
            CrawlingSuspended = 0;

            foreach (var dir in _settings.CrawlingBaseDirs)
            {
                Create(new List<string> { dir }, once: false);
            }
            _logger?.LogTrace("Leave: Crawler()");
        }

        /// <summary>
        /// クローラ開始
        /// </summary>
        public void Create(List<string> dirs, bool once = false)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}, {dirs}, {once}",
                    System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "",
                    String.Join(',', dirs),
                    once);

                var trigger = new AutoResetEvent(false);
                var thread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            _logger?.LogInformation("Crawling {dirs} Start", String.Join(',', dirs));
                            var sw = Stopwatch.StartNew();
                            foreach (var dir in dirs)
                            {
                                CrawlingDir(dir, _db.GetDirectryFromDirPath(dir), _cts.Token);
                            }
                            PurgeMissingFiles(dirs, _cts.Token);
                            if (_cts.Token.IsCancellationRequested)
                            {
                                _logger?.LogInformation("Crawling {dirs} Canceled", String.Join(',', dirs));
                                return;
                            }
                            if (once)
                            {
                                _logger?.LogInformation("Crawling {dirs} Finished in ({time}), crawling once", String.Join(',', dirs), sw.Elapsed);
                                break;
                            }
                            var wt = _settings.CrawlingInterval * 1000 < sw.ElapsedMilliseconds ? 0 : _settings.CrawlingInterval * 1000 - sw.ElapsedMilliseconds;
                            _logger?.LogInformation("Crawling {dirs} Finished in ({time}), Waiting {wait} sec",
                                String.Join(',', dirs), sw.Elapsed, wt / 1000);
                            // インターバル経過、手動トリガー、キャンセルのいずれかまで待機
                            WaitHandle.WaitAny(new WaitHandle[] { _cts.Token.WaitHandle, trigger }, (int)Math.Min(wt, int.MaxValue - 1));
                            if (_cts.Token.IsCancellationRequested)
                            {
                                _logger?.LogInformation("Crawling {dirs} Canceled", String.Join(',', dirs));
                                return;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger?.LogInformation("Crawling {dirs} Canceled", String.Join(',', dirs));
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Exception caught in Crawler: {ex}", ex);
                            if (_cts.Token.WaitHandle.WaitOne(60 * 1000)) return;
                        }
                    }
                })
                {
                    Priority = ThreadPriority.Lowest,
                    IsBackground = true,
                };
                thread.Start();
                _threads.Add(thread);
                lock (_triggerEvents) _triggerEvents.Add(trigger);
            }
            catch (Exception ex) { _logger?.LogError("Exception caught: {ex}", ex.ToString()); }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// 全クローラの待機を解除し、即座にクローリングを開始する
        /// </summary>
        public void TriggerNow()
        {
            _logger?.LogInformation("Crawling triggered manually");
            lock (_triggerEvents)
            {
                foreach (var trigger in _triggerEvents)
                    trigger.Set();
            }
        }

        void CrawlingDir(string dir, DirectoryDbEntry dirDbEntry, CancellationToken token)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}, {dir}, {dirId}",
                    System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "",
                    dir, dirDbEntry?.Id);

                if (token.IsCancellationRequested) return;
                if (!Directory.Exists(dir))
                {
                    _logger?.LogInformation("Dir \"{dir}\" not found", dir);
                    return;
                }
                if (_settings.IsExcluded(dir))
                {
                    _logger?.LogInformation("Dir \"{dir}\" is excluded", dir);
                    return;
                }

                // ディレクトリ探索
                Dictionary<string, DirectoryDbEntry> subDirDbEntries = null;
                if (dirDbEntry != null)
                {
                    subDirDbEntries = new Dictionary<string, DirectoryDbEntry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in _db.GetChildDirectories(dirDbEntry.Id))
                        subDirDbEntries[entry.Name] = entry;
                }
                foreach (var subdir in Directory.EnumerateDirectories(dir, "*", new EnumerationOptions()))
                {
                    if (token.IsCancellationRequested) return;
                    DirectoryDbEntry subDirDbEntry = null;
                    subDirDbEntries?.TryGetValue(Path.GetFileName(subdir), out subDirDbEntry);
                    CrawlingDir(subdir, subDirDbEntry, token);
                }

                // ファイル探索
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    if (token.IsCancellationRequested) return;

                    // CrawlingSuspended > 0ならクローリング一時停止
                    if (CrawlingSuspended > 0)
                    {
                        _logger?.LogInformation("Crawling {dir} Suspended", dir);
                        while (CrawlingSuspended > 0)
                        {
                            if (token.WaitHandle.WaitOne(1000)) return;
                        }
                        _logger?.LogInformation("Crawling {dir} Resumed", dir);
                    }

                    var backupTime = DateTime.Now;
                    var fileAttr = new AttributeFileEntry(file);

                    // 既にクローリング済みなら次のファイルへ
                    if (_checkedFiles.TryGetValue(file, out var checkedAttr) &&
                        checkedAttr.CreationTime == fileAttr.CreationTime &&
                            checkedAttr.LastWriteTime == fileAttr.LastWriteTime &&
                            checkedAttr.Size == fileAttr.Size)
                        continue;

                    // 最後に更新されてからBackupInterval未経過なら、まだクローリング済みとせず
                    // 次回以降のクロールで再チェックする
                    // (ここで_checkedFilesに登録すると、以後更新されないファイルが永久にバックアップされない)
                    if (backupTime - fileAttr.LastUpdate < TimeSpan.FromSeconds(_settings.BackupInterval(file)))
                        continue;
                    _checkedFiles[file] = fileAttr;
                    var dbFile = _db.GetFile(file, dirDbEntry);
                    AttributeDbEntry dbAttr = null;
                    if (dbFile != null)
                    {
                        dbAttr = _db.GetAttribute(dbFile.Id, fileAttr.CreationTime, fileAttr.LastWriteTime, fileAttr.Size);
                        if (dbAttr != null) continue;
                    }

                    // バックアップコピー
                    _logger.LogDebug("File found: {file}", file);
                    _backupScheduler.Add(file, dbAttr, fileAttr, SchedulePriority.Low);
                }
            }
            catch (Exception ex) { _logger?.LogError("Exception caught: {ex}", ex.ToString()); }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// ディスク上に存在しなくなったファイルを_checkedFilesから削除する
        /// (削除されたファイルのエントリが無制限に溜まるのを防ぐ)
        /// </summary>
        void PurgeMissingFiles(List<string> dirs, CancellationToken token)
        {
            try
            {
                foreach (var kv in _checkedFiles)
                {
                    if (token.IsCancellationRequested) return;
                    // このクローラが担当するディレクトリ配下のみ対象
                    if (!dirs.Any(d => kv.Key.StartsWith(d, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!File.Exists(kv.Key))
                        _checkedFiles.TryRemove(kv.Key, out _);
                }
            }
            catch (Exception ex) { _logger?.LogError("Exception caught: {ex}", ex.ToString()); }
        }

        /// <summary>
        /// Fileエントリ数の取得
        /// </summary>
        public int FileCount()
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                return _checkedFiles.Count;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Exception caught: {ex}", ex.ToString());
                return 0;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        public void Dispose()
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                _cts.Cancel();
                foreach (var thread in _threads)
                {
                    thread.Join();
                }
                GC.SuppressFinalize(this);
            }
            catch (Exception ex) { _logger?.LogError("Exception caught: {ex}", ex.ToString()); }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }
    }
}
