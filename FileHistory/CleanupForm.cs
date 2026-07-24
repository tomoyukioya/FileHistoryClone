using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileHistory
{
    public partial class CleanupForm : Form
    {
        // DI
        readonly Settings _settings;
        readonly IBackupDb _db;
        readonly Crawler _crawler;
        readonly ILogger<CleanupForm> _logger;

        // Worker
        Task _progressWorker;
        Task _cleanupWorker;

        //
        CancellationTokenSource _cts;
        bool _running = false;
        int _scanCount;
        int _filesToScan;
        int _deleteCount;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="db"></param>
        public CleanupForm(Settings settings, IBackupDb db, Crawler crawler, ILoggerFactory loggerFactory)
        {
            try
            {
                _logger = loggerFactory.CreateLogger<CleanupForm>();
                _logger.LogTrace("Enter: CleanupForm()");

                _settings = settings;
                _db = db;
                _crawler = crawler;

                InitializeComponent();

                Text = Strings.Get("Cleanup_Title");
                label1.Text = Strings.Get("Cleanup_ScannedFiles");
                label2.Text = Strings.Get("Cleanup_DeletedFiles");
                buttonClose.Text = Strings.Get("Cleanup_ButtonClose");
                buttonStartStop.Text = Strings.Get("Cleanup_ButtonStart");
                comboBox.Items.Add(Strings.Get("Cleanup_ModeKeepAllLatest"));
                comboBox.Items.Add(Strings.Get("Cleanup_ModeKeepExistingLatest"));
                comboBox.SelectedIndex = 0;
                labelNote.Text = Strings.Get("Cleanup_TemporaryNote");
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Exception caught in CleanupForm(): {ex}");
                throw;
            }
            finally
            {
                _logger.LogTrace("Exit: CleanupForm()");
            }
        }

        /// <summary>
        /// フォーム破棄後のInvokeによる例外を防ぎつつUIスレッドで実行する
        /// </summary>
        void SafeInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// ファイルクリーンアップワーカー
        /// (全ての最新のみ残す)
        /// </summary>
        async Task CleanupWorker_PreserveAllLatest()
        {
            try
            {
                _logger.LogTrace("Enter: CleanupWorker_PreserveAllLatest()");
                Interlocked.Increment(ref _crawler.CrawlingSuspended);

                var fileCountTask = _db.FileCount(_cts.Token);
                var attrCountTask = _db.AttributeCount(_cts.Token);
                await fileCountTask;
                await attrCountTask;
                _filesToScan = fileCountTask.Result;
                var filesToDelete = attrCountTask.Result - fileCountTask.Result;

                if (filesToDelete == 0)
                {
                    // 削除ファイルがなければすぐに終了
                    _logger.LogInformation("filesToDelete == 0, exit.");
                    _scanCount = _filesToScan;
                }
                else
                {
                    // ファイルスキャン
                    foreach (var file in _db.FindAllFiles())
                    {
                        _scanCount++;
                        if (_cts.IsCancellationRequested) break;
                        var backupFileDir = _db.GetFileDir(file.Id);
                        foreach (var attr in _db.GetAttributes(file.Id).OrderByDescending(m => m.BackupTime).Skip(1))
                        {
                            _deleteCount++;
                            if (_cts.IsCancellationRequested) break;
                            var backupAttributeFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, file.Name), attr.BackupTime);
                            _logger.LogInformation($"Cleanup {backupAttributeFullPath}");
                            try
                            {
                                if (File.Exists(backupAttributeFullPath))
                                    File.Delete(backupAttributeFullPath);
                                _db.DeleteAttribute(attr.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"Exception caught in deleting {backupAttributeFullPath}: {ex}");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"OperationCanceledException in CleanupWorker_PreserveAllLatest()");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in CleanupWorker_PreserveAllLatest(): {ex}");
            }
            finally
            {
                // クリーンアップ終了
                _cleanupWorker = null;
                await TaskCancelAndCleanupGui();
                Interlocked.Decrement(ref _crawler.CrawlingSuspended);
                _logger.LogTrace("Leave: CleanupWorker_PreserveAllLatest()");
            }
        }

        /// <summary>
        /// ファイルクリーンアップワーカー
        /// (既存ファイルの最新のみ残す)
        /// </summary>
        async Task CleanupWorker_PreserveExistingLatest()
        {
            try
            {
                _logger.LogTrace("Enter: CleanupWorker_PreserveExistingLatest()");
                Interlocked.Increment(ref _crawler.CrawlingSuspended);
                _filesToScan = await _db.FileCount(_cts.Token);

                foreach (var file in _db.FindAllFiles())
                {
                    _scanCount++;
                    if (_cts.IsCancellationRequested) break;
                    var backupFileDir = _db.GetFileDir(file.Id);
                    var filePath = Path.Combine(backupFileDir, file.Name);
                    var fileExists = File.Exists(filePath);
                    var isExcluded = _settings.IsExcluded(backupFileDir);
                    _logger.LogTrace($"Checking {filePath}, FileExists = {fileExists}, IsExcluded = {isExcluded}");
                    if (fileExists && !isExcluded)
                    {
                        // ファイルが存在してExcludeではない場合は、最新のバックアップのみ残す
                        foreach (var attr in _db.GetAttributes(file.Id).OrderByDescending(m => m.BackupTime).Skip(1))
                        {
                            _deleteCount++;
                            if (_cts.IsCancellationRequested) break;
                            var backupAttributeFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, file.Name), attr.BackupTime);
                            _logger.LogInformation($"Cleanup {backupAttributeFullPath}");
                            try
                            {
                                if (File.Exists(backupAttributeFullPath))
                                    File.Delete(backupAttributeFullPath);
                                _db.DeleteAttribute(attr.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"Exception caught in deleting {backupAttributeFullPath}: {ex}");
                            }
                        }
                    }
                    else
                    {
                        // ファイルが存在しない場合、もしくはExcludeの場合、全てのバックアップを削除

                        // アトリビュートを全て削除
                        foreach (var attr in _db.GetAttributes(file.Id))
                        {
                            _deleteCount++;
                            if (_cts.IsCancellationRequested) break;
                            var backupAttributeFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, file.Name), attr.BackupTime);
                            _logger.LogInformation($"Cleanup {backupAttributeFullPath}");
                            try
                            {
                                if (File.Exists(backupAttributeFullPath))
                                    File.Delete(backupAttributeFullPath);
                                _db.DeleteAttribute(attr.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"Exception caught in deleting {backupAttributeFullPath}: {ex}");
                            }
                        }

                        // ファイルエントリーを削除
                        _logger.LogTrace($"Remove file entry {file.Id} from DB");
                        _db.DeleteFile(file.Id);

                        // ディレクトリが空になったら削除
                        _db.DeleteDirectoryIfEmpty(file.DirectoryId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"OperationCanceledException in CleanupWorker_PreserveExistingLatest()");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in CleanupWorker_PreserveExistingLatest(): {ex}");
            }
            finally
            {
                // クリーンアップ終了
                _cleanupWorker = null;
                await TaskCancelAndCleanupGui();
                Interlocked.Decrement(ref _crawler.CrawlingSuspended);
                _logger.LogTrace("Leave: CleanupWorker_PreserveExistingLatest()");
            }
        }

        /// <summary>
        /// プログレスバー表示ワーカー
        /// </summary>
        async Task ProgressWorker()
        {
            try
            {
                _logger.LogTrace("Enter: ProgressWorker()");

                // GUI初期化
                SafeInvoke(() =>
                {
                    progressBar1.Value = 0;
                    ScanCountTextBox.Text = "";
                    DeleteCountTextBox.Text = "";
                });

                // CleanupWorkerで_filesToScanが設定されるのを待つ
                while (_filesToScan == -1)
                    await Task.Delay(100, _cts.Token);

                while (!_cts.IsCancellationRequested)
                {
                    SafeInvoke(() =>
                    {
                        if (_scanCount > 0 && _filesToScan > 0)
                        {
                            ScanCountTextBox.Text = $"{_scanCount.ToString("#,0")} / {_filesToScan.ToString("#,0")}";
                            progressBar1.Value = Math.Min(100, (_scanCount * 100) / _filesToScan);
                        }
                        DeleteCountTextBox.Text = _deleteCount.ToString("#,0");
                    });
                    await Task.Delay(1000, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"OperationCanceledException in ProgressWorker()");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in ProgressWorker(): {ex}");
            }
            finally
            {
                _logger.LogTrace("Leave: ProgressWorker()");
            }
        }

        /// <summary>
        /// 閉じるボタン
        /// </summary>
        private void buttonClose_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogTrace("Enter: buttonClose_Click()");
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in buttonClose_Click(): {ex}");
            }
            finally
            {
                _logger.LogTrace("Leave: buttonClose_Click()");
            }
        }

        /// <summary>
        /// 削除/停止ボタン
        /// </summary>
        private async void buttonStartStop_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogTrace("Enter: buttonStartStop_Click()");

                // 停止
                if (_running)
                {
                    await TaskCancelAndCleanupGui();
                    return;
                }

                // 削除開始
                _running = true;
                _filesToScan = -1;
                _deleteCount = 0;
                _cts = new CancellationTokenSource();
                comboBox.Enabled = false;
                buttonStartStop.Text = Strings.Get("Cleanup_ButtonStop");
                buttonClose.Enabled = false;

                // ワーカー起動
                // (Task.Runはasyncデリゲートをアンラップするため、
                //  ワーカー全体の完了を_progressWorker/_cleanupWorkerで待機できる)
                _progressWorker = Task.Run(() => ProgressWorker());
                if (comboBox.SelectedIndex == 0)
                    _cleanupWorker = Task.Run(() => CleanupWorker_PreserveAllLatest());
                else
                    _cleanupWorker = Task.Run(() => CleanupWorker_PreserveExistingLatest());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in buttonStartStop_Click(): {ex}");
            }
            finally
            {
                _logger.LogTrace("Leave: buttonStartStop_Click()");
            }
        }

        /// <summary>
        /// _progressWorkerと_cleanupWorkerをキャンセルしてGUIを元に戻す
        /// </summary>
        async Task TaskCancelAndCleanupGui()
        {
            try
            {
                _cts.Cancel();
                if (_progressWorker != null)
                {
                    await _progressWorker.ConfigureAwait(false);
                    _progressWorker = null;
                }
                if (_cleanupWorker != null)
                {
                    await _cleanupWorker.ConfigureAwait(false);
                    _cleanupWorker = null;
                }
            }
            catch { }

            var action = new Action(() =>
            {
                comboBox.Enabled = true;
                buttonStartStop.Text = Strings.Get("Cleanup_ButtonStart");
                buttonClose.Enabled = true;
                ScanCountTextBox.Text = "";
                DeleteCountTextBox.Text = "";
                progressBar1.Value = 0;
            });
            if (InvokeRequired)
                SafeInvoke(action);
            else
                action();
            _running = false;
        }
    }
}
