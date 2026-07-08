using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileHistory
{
    public class DirectoryWatcher: IDisposable
    {
        readonly Settings _settings;
        readonly ILogger _logger;
        readonly IBackupScheduler _backupScheduler;
        readonly List<FileSystemWatcher> _watchers;

        public DirectoryWatcher(Settings settings, ILoggerFactory loggerFactory, IBackupScheduler backupScheduler)
        {
            _settings = settings;
            _logger = loggerFactory.CreateLogger<DirectoryWatcher>();
            _backupScheduler = backupScheduler;
            _watchers = new List<FileSystemWatcher>();

            // ファイル監視
            foreach(var dir in _settings.CrawlingBaseDirs)
            {
                // IncludeDirsにファイルパスや存在しないパスが混ざっていても落ちないようにスキップ
                if (!Directory.Exists(dir))
                {
                    _logger.LogWarning($"IncludeDir is not an existing directory, skipped: {dir}");
                    continue;
                }

                try
                {
                    var watcher = new FileSystemWatcher();
                    watcher.Path = dir;
                    // LastAccessはノイズが多いため監視しない
                    watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;
                    watcher.Filter = "";
                    watcher.IncludeSubdirectories = true;
                    // 既定の8KBでは大量変更時にバッファオーバーフローするため最大値に拡大
                    watcher.InternalBufferSize = 64 * 1024;

                    watcher.Changed += new FileSystemEventHandler(FileChanged);
                    watcher.Created += new FileSystemEventHandler(FileChanged);
                    watcher.Deleted += new FileSystemEventHandler(FileChanged);
                    watcher.Renamed += new RenamedEventHandler(FileRenamed);
                    watcher.Error += new ErrorEventHandler(WatcherError);

                    watcher.EnableRaisingEvents = true;

                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to start FileSystemWatcher for \"{dir}\": {ex}");
                }
            }
        }

        public void Dispose()
        {
            foreach(var watcher in _watchers)
            {
                watcher.Dispose();
            }
        }

        void FileChanged(Object source, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                    _logger.LogDebug($"File Changed: {e.FullPath}");
                    _backupScheduler.Add(e.FullPath, null, null, SchedulePriority.High);
                    break;
                case WatcherChangeTypes.Created:
                    _logger.LogDebug($"File Created : {e.FullPath}");
                    _backupScheduler.Add(e.FullPath, null, null, SchedulePriority.High);
                    break;
                case WatcherChangeTypes.Deleted:
                    // 削除ファイルはバックアップ対象にならないためログのみ
                    _logger.LogDebug($"File Deleted : {e.FullPath}");
                    break;
            }
        }

        void FileRenamed(System.Object source, RenamedEventArgs e)
        {
            _logger.LogDebug($"File Renamed : {e.OldFullPath} -> {e.FullPath}");
            _backupScheduler.Add(e.FullPath, null, null, SchedulePriority.High);
        }

        /// <summary>
        /// バッファオーバーフロー等の監視エラーから回復する
        /// (取りこぼしたイベントは次回クロールで補足される)
        /// </summary>
        void WatcherError(object sender, ErrorEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;
            _logger.LogError($"FileSystemWatcher error on \"{watcher?.Path}\": {e.GetException()}");
            if (watcher == null) return;
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.EnableRaisingEvents = true;
                _logger.LogInformation($"FileSystemWatcher for \"{watcher.Path}\" restarted.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to restart FileSystemWatcher for \"{watcher.Path}\": {ex}");
            }
        }
    }
}
