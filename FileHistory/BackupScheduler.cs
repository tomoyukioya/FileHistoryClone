using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHistory
{
    public interface IBackupScheduler
    {
        void Add(string path, AttributeDbEntry dbAttr, AttributeFileEntry fileAttr, SchedulePriority priority);
        void Dispose();
    }

    public class BackupScheduler : IBackupScheduler, IDisposable
    {
        // DI
        Settings _settings { get; set; }
        ILogger _logger { get; set; }
        IBackupDb _db { get; set; }

        // Task Schedule
        SortedList<DateTime, List<ScheduleItem>> _highPrioritySchedules { get; set; }
        object _highPrioritySchedulesLock { get; set; }
        ManualResetEvent _highPrioritySchedulesAddEvent { get; set; }

        List<KeyValuePair<DateTime, ScheduleItem>> _lowPrioritySchedules { get; set; }
        SemaphoreSlim _lowPrioritySchedulesSemaphore { get; set; }
        object _lowPrioritySchedulesLock { get; set; }
        readonly int MAX_LOW_SCHEDULE = 100;

        // Copy Worker
        Dictionary<string, Task> _copyWorkers;
        ManualResetEvent _copyWorkersExitEvent { get; set; }
        readonly int MAX_COPY_WORKER = 10;

        // Worker管理
        CancellationTokenSource _cts { get; set; }
        Thread _thread { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="logger"></param>
        /// <param name="db"></param>
        public BackupScheduler(Settings settings, ILoggerFactory loggerFactory, IBackupDb db)
        {
            _settings = settings;
            _logger = loggerFactory.CreateLogger<BackupScheduler>();
            _db = db;

            _highPrioritySchedules = new SortedList<DateTime, List<ScheduleItem>>();
            _highPrioritySchedulesLock = new object();
            _highPrioritySchedulesAddEvent = new ManualResetEvent(false);
            _lowPrioritySchedules = new List<KeyValuePair<DateTime, ScheduleItem>>(MAX_LOW_SCHEDULE);
            _lowPrioritySchedulesSemaphore = new SemaphoreSlim(MAX_LOW_SCHEDULE, MAX_LOW_SCHEDULE);
            _lowPrioritySchedulesLock = new object();

            _copyWorkersExitEvent = new ManualResetEvent(false);

            _cts = new CancellationTokenSource();

            _thread = new Thread(new ThreadStart(() => CopyWorkerController(_cts.Token))) { Priority = ThreadPriority.Lowest, };
            _thread.Start();
        }

        /// <summary>
        /// バックアップスケジュール登録
        /// </summary>
        /// <param name="path"></param>
        /// <param name="attr"></param>
        /// <param name="priority"></param>
        public void Add(string path, AttributeDbEntry dbAttr, AttributeFileEntry fileAttr, SchedulePriority priority)
        {
            if (priority == SchedulePriority.High)
            {
                // Highプライオリティの場合、スケジュール投入してすぐにリターン
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (_settings.IsExcluded(path)) return;

                        if (fileAttr == null)
                            fileAttr = new AttributeFileEntry(path);
                        if (!fileAttr.IsValid)
                            return;

                        var backupAt = fileAttr.LastUpdate + TimeSpan.FromSeconds(_settings.BackupInterval(path));
                        var scheduleItem = new ScheduleItem
                        {
                            FileDbEntry = new FileDbEntry { Name = path },
                            AttributeDbEntry = new AttributeDbEntry(fileAttr),
                        };

                        _logger.LogDebug($"Schedule(High) backup at {backupAt}: {path}");
                        lock (_highPrioritySchedulesLock)
                        {
                            if (_highPrioritySchedules.ContainsKey(backupAt))
                                _highPrioritySchedules[backupAt].Add(scheduleItem);
                            else
                                _highPrioritySchedules.Add(backupAt, new List<ScheduleItem> { scheduleItem });
                            _highPrioritySchedulesAddEvent.Set();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Exception caught in Add(High) for \"{path}\": {ex}");
                    }
                });
            }
            else
            {
                // Lowプライオリティの場合
                DateTime backupAt;
                if (dbAttr == null)
                    backupAt = fileAttr.LastUpdate + TimeSpan.FromSeconds(_settings.BackupInterval(path));
                else
                    backupAt = dbAttr.LastUpdate + TimeSpan.FromSeconds(_settings.BackupInterval(path));

                // MAX_LOW_SCHEDULEになるまでブロック
                try
                {
                    _lowPrioritySchedulesSemaphore.Wait(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                _logger.LogDebug($"Shcedule(Low) backup at {backupAt}: {path}");
                lock (_lowPrioritySchedulesLock)
                {
                    _lowPrioritySchedules.Add(new KeyValuePair<DateTime, ScheduleItem>(
                         backupAt,
                         new ScheduleItem
                         {
                             FileDbEntry = new FileDbEntry { Name = path },
                             AttributeDbEntry = new AttributeDbEntry(fileAttr),
                         }));
                }
            }
        }

        void CopyWorkerController(CancellationToken token)
        {
            _copyWorkers = new Dictionary<string, Task>();
            while (true)
            {
                try
                {

                    if (token.IsCancellationRequested) break;

                    // Highプライオリティタスクがあれば全て起動
                    var snapshot = new List<KeyValuePair<DateTime, List<ScheduleItem>>>();
                    lock (_highPrioritySchedulesLock)
                        foreach (var item in _highPrioritySchedules)
                        {
                            if (DateTime.Now < item.Key) break;
                            snapshot.Add(item);
                        }
                    if (snapshot.Count > 0)
                    {

                        foreach (var items in snapshot)
                        {
                            if (token.IsCancellationRequested) break;

                            foreach (var item in items.Value.ToArray())
                            {
                                if (token.IsCancellationRequested) break;

                                // 既にコピー中であれば何もしない
                                if (_copyWorkers.ContainsKey(item.FileDbEntry.Name))
                                {
                                    _logger.LogDebug($"{item.FileDbEntry.Name} is under copying, skip.");
                                    lock (_highPrioritySchedulesLock)
                                    {
                                        items.Value.Remove(item);
                                        if (items.Value.Count == 0) _highPrioritySchedules.Remove(items.Key);
                                    }
                                    continue;
                                }

                                // 現時点でのディスク上の属性
                                var fileAttr = new AttributeFileEntry(item.FileDbEntry.Name);

                                // 現時点での最新のDBバックアップ情報
                                var dbFile = _db.GetFile(item.FileDbEntry.Name);
                                var dbAttr = dbFile == null ? null : _db.GetLatestAttribute(dbFile.Id);

                                // 既にバックアップされていれば何もしない
                                if (dbAttr != null && fileAttr.LastUpdate == dbAttr.LastUpdate)
                                {
                                    _logger.LogDebug($"{item.FileDbEntry.Name} already copied, skip.");
                                    lock (_highPrioritySchedulesLock)
                                    {
                                        items.Value.Remove(item);
                                        if (items.Value.Count == 0) _highPrioritySchedules.Remove(items.Key);
                                    }
                                    continue;
                                }

                                if (item.AttributeDbEntry.LastUpdate == fileAttr.LastUpdate)
                                {
                                    // ファイルが更新されていなければコピー
                                    _copyWorkers.Add(item.FileDbEntry.Name, Task.Factory.StartNew(() => CopyTask(item.FileDbEntry.Name, fileAttr, dbFile, token)));
                                    lock (_highPrioritySchedulesLock)
                                    {
                                        items.Value.Remove(item);
                                        if (items.Value.Count == 0) _highPrioritySchedules.Remove(items.Key);
                                    }
                                }
                                else
                                {
                                    // ファイルが更新されていた場合、スケジューラに再登録
                                    var backupAt = fileAttr.LastUpdate + TimeSpan.FromSeconds(_settings.BackupInterval(item.FileDbEntry.Name));
                                    var scheduleItem = new ScheduleItem
                                    {
                                        FileDbEntry = new FileDbEntry { Name = item.FileDbEntry.Name },
                                        AttributeDbEntry = new AttributeDbEntry(fileAttr),
                                    };

                                    _logger.LogDebug($"{item.FileDbEntry.Name} modified, reschedule at {backupAt}");
                                    lock (_highPrioritySchedulesLock)
                                    {
                                        items.Value.Remove(item);
                                        if (items.Value.Count == 0) _highPrioritySchedules.Remove(items.Key);

                                        if (_highPrioritySchedules.ContainsKey(backupAt))
                                            _highPrioritySchedules[backupAt].Add(scheduleItem);
                                        else
                                            _highPrioritySchedules.Add(backupAt, new List<ScheduleItem> { scheduleItem });
                                        _highPrioritySchedulesAddEvent.Set();
                                    }
                                }
                            }
                        }
                    }

                    // Workerクリーンアップ
                    foreach (var item in _copyWorkers.ToArray())
                    {
                        if (item.Value.Status == TaskStatus.Canceled ||
                        item.Value.Status == TaskStatus.Faulted ||
                        item.Value.Status == TaskStatus.RanToCompletion)
                            _copyWorkers.Remove(item.Key);
                    }

                    // Lowプライオリティタスクは、タスク数がMAX_LOW_SCHEDULE以下の場合のみ起動
                    lock (_lowPrioritySchedulesLock)
                    {
                        if (_lowPrioritySchedules.Count > 0 && _copyWorkers.Count < MAX_COPY_WORKER)
                        {
                            var tasks = _lowPrioritySchedules.Where(m => m.Key <= DateTime.Now);
                            if (tasks.Any())
                            {
                                // 起動するタスクあり
                                var task = tasks.OrderBy(m => m.Key).First();

                                // 既にコピー中であれば何もしない
                                if (_copyWorkers.ContainsKey(task.Value.FileDbEntry.Name))
                                {
                                    _logger.LogDebug($"{task.Value.FileDbEntry.Name} is under copying, skip.");
                                    _lowPrioritySchedules.Remove(task);
                                    _lowPrioritySchedulesSemaphore.Release();
                                }
                                else
                                {
                                    // 現時点でのディスク上の属性
                                    var fileAttr = new AttributeFileEntry(task.Value.FileDbEntry.Name);

                                    // 現時点での最新のDBバックアップ情報
                                    var dbFile = _db.GetFile(task.Value.FileDbEntry.Name);
                                    var dbAttr = dbFile == null ? null : _db.GetLatestAttribute(dbFile.Id);

                                    if (dbAttr != null && fileAttr.LastUpdate == dbAttr.LastUpdate)
                                    {
                                        // 既にバックアップされていれば何もしない
                                        _logger.LogDebug($"{task.Value.FileDbEntry.Name} already copied, skip.");
                                        _lowPrioritySchedules.Remove(task);
                                        _lowPrioritySchedulesSemaphore.Release();
                                    }
                                    else if (task.Value.AttributeDbEntry.LastUpdate == fileAttr.LastUpdate)
                                    {
                                        // ファイルが更新されていなければコピー
                                        _copyWorkers.Add(task.Value.FileDbEntry.Name, Task.Factory.StartNew(() => CopyTask(task.Value.FileDbEntry.Name, fileAttr, dbFile, token)));
                                        _lowPrioritySchedules.Remove(task);
                                        _lowPrioritySchedulesSemaphore.Release();
                                    }
                                    else
                                    {
                                        // ファイルが更新されていた場合、スケジューラに再登録
                                        var backupAt = fileAttr.LastUpdate + TimeSpan.FromSeconds(_settings.BackupInterval(task.Value.FileDbEntry.Name));
                                        var scheduleItem = new ScheduleItem
                                        {
                                            FileDbEntry = new FileDbEntry { Name = task.Value.FileDbEntry.Name },
                                            AttributeDbEntry = new AttributeDbEntry(fileAttr),
                                        };
                                        _logger.LogDebug($"{task.Value.FileDbEntry.Name} modified, reschedule at {backupAt}");
                                        _lowPrioritySchedules.Remove(task);
                                        _lowPrioritySchedules.Add(new KeyValuePair<DateTime, ScheduleItem>(backupAt, scheduleItem));
                                    }
                                }
                            }
                        }
                    }

                    // 待機
                    // 次にスケジュールされたアイテムの時刻・追加イベント・ワーカー終了イベントの
                    // いずれか早いものまで待つ（実行可能なアイテムがあれば待たずに抜ける）
                    while (true)
                    {
                        _highPrioritySchedulesAddEvent.Reset();
                        _copyWorkersExitEvent.Reset();

                        var now = DateTime.Now;
                        var timeout = 1000.0;

                        bool highReady;
                        lock (_highPrioritySchedulesLock)
                        {
                            highReady = _highPrioritySchedules.Count > 0 && _highPrioritySchedules.Keys[0] <= now;
                            if (!highReady && _highPrioritySchedules.Count > 0)
                                timeout = Math.Min(timeout, (_highPrioritySchedules.Keys[0] - now).TotalMilliseconds + 1);
                        }
                        if (highReady) break;

                        bool lowReady = false;
                        lock (_lowPrioritySchedulesLock)
                        {
                            if (_lowPrioritySchedules.Count > 0)
                            {
                                var nextLow = _lowPrioritySchedules.Min(m => m.Key);
                                if (nextLow <= now) lowReady = true;
                                else timeout = Math.Min(timeout, (nextLow - now).TotalMilliseconds + 1);
                            }
                        }
                        if (lowReady && _copyWorkers.Count(m => m.Value.Status == TaskStatus.Running) < MAX_COPY_WORKER)
                            break;

                        if (WaitHandle.WaitTimeout != WaitHandle.WaitAny(new WaitHandle[] { _highPrioritySchedulesAddEvent, _copyWorkersExitEvent }, (int)Math.Max(1, timeout)))
                            break;
                        if (token.IsCancellationRequested) break;
                    }
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        _logger.LogInformation($"BackupScheduler Canceled");
                        return;
                    }
                    _logger.LogError($"Exception caught in CopyWorkerController: {ex}");
                    if (token.WaitHandle.WaitOne(60 * 1000)) return;
                }
            }
        }

        void CopyTask(string file, AttributeFileEntry fileAttr, FileDbEntry dbFile, CancellationToken token)
        {
            var now = DateTime.Now;
            _logger.LogInformation($"Backup {file}");

            var backupFile = BackupDb.BackupFileName(_settings.DataDir, file, now);
            Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
            try
            {
                using (var infs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var outfs = new FileStream(backupFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // GetAwaiter().GetResult()で例外をAggregateExceptionに包まず伝播させる
                    // (下のcatchのFileNotFoundException判定を機能させるため)
                    infs.CopyToAsync(outfs, token).GetAwaiter().GetResult();
                }

                // コピー先タイムスタンプ設定
                File.SetCreationTime(backupFile, fileAttr.CreationTime);
                File.SetLastWriteTime(backupFile, fileAttr.LastWriteTime);
                File.SetLastAccessTime(backupFile, fileAttr.LastAccessTime);

                // DB追加
                if (dbFile == null) dbFile = _db.InsertFile(file);
                _db.InsertAttribute(dbFile.Id, now, fileAttr.CreationTime, fileAttr.LastWriteTime, fileAttr.LastAccessTime, fileAttr.Size);
            }
            catch (Exception ex)
            {
                // 例外が発生したらロールバック
                if (token.IsCancellationRequested)
                    _logger.LogInformation($"Copy task canceled \"{file}\" to \"{backupFile}\"");
                else if (ex is FileNotFoundException)
                    _logger.LogDebug($"File \"{file}\" not found, skip backup.");
                else
                    _logger.LogError($"Exception caught in copy \"{file}\" to \"{backupFile}\": {ex}");

                if (File.Exists(backupFile))
                    try
                    {
                        File.Delete(backupFile);
                    }
                    catch (Exception) { }
                if (dbFile != null)
                {
                    var dbAttribute = _db.GetAttribute(dbFile.Id, fileAttr.CreationTime, fileAttr.LastWriteTime, fileAttr.Size);
                    if (dbAttribute != null) _db.DeleteAttribute(dbAttribute.Id);
                }

                if (token.IsCancellationRequested) return;
            }
            finally
            {
                _copyWorkersExitEvent.Set();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                Task.WaitAll(_copyWorkers?.Values.ToArray() ?? Array.Empty<Task>());
            }
            catch (AggregateException) { }
            _thread.Join();
        }
    }

    public class ScheduleItem
    {
        public AttributeDbEntry AttributeDbEntry { get; set; }
        public FileDbEntry FileDbEntry { get; set; }
        public DirectoryDbEntry DirectoryDbEntry { get; set; }
    }

    public enum SchedulePriority
    {
        High,
        Low,
    }
}
