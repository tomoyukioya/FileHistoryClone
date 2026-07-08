using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileHistory
{
    public partial class MainForm : Form
    {
        Settings _settings;
        IBackupDb _db;
        Crawler _crawler;
        ILoggerFactory _loggerFactory;
        ILogger _logger;
        Task _fileCountUpdateTask;
        Task _fileCountCrawlingTask;
        CancellationTokenSource _cts;
        public static MainForm Instance;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainForm(Settings settings, IBackupDb db, ILoggerFactory loggerFactory, Crawler crawler)
        {
            _settings = settings;
            _db = db;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<MainForm>();
            _cts = new CancellationTokenSource();
            _crawler = crawler;
            Instance = this;

            InitializeComponent();
            ApplyLocalization();
        }

        /// <summary>
        /// フォーム破棄後のInvokeによる例外を防ぎつつUIスレッドで実行する
        /// </summary>
        void SafeInvoke(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// UI文字列をリソースから設定
        /// </summary>
        void ApplyLocalization()
        {
            Text = Strings.Get("MainForm_Title");
            label1.Text = Strings.Get("MainForm_BackedUpFileCount");
            label2.Text = Strings.Get("MainForm_CrawledFileCount");
            button_cleanup.Text = Strings.Get("MainForm_ButtonCleanup");
        }

        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // 「バックアップ済みファイル数」更新タスク登録
            _fileCountUpdateTask = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        if (_db != null)
                        {
                            var count = await _db.FileCount(_cts.Token).ConfigureAwait(false);
                            SafeInvoke(() => { fileCount.Text = count.ToString("#,0"); });
                        }
                        await Task.Delay(10000, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_cts.IsCancellationRequested)
                            _logger.LogError($"Exception caught in FileCountUpdateTask: {ex}");
                        break;
                    }
                }
                _logger.LogDebug($"FileCountUpdateTask End");
            });

            // 「クローリング済みファイル数」更新タスク登録
            _fileCountCrawlingTask = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        if (_crawler != null)
                        {
                            var count = _crawler.FileCount();
                            SafeInvoke(() => { crawlingCount.Text = count.ToString("#,0"); });
                        }
                        await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!_cts.IsCancellationRequested)
                            _logger.LogError($"Exception caught in FileCountCrawlingTask: {ex}");
                        break;
                    }
                }
                _logger.LogDebug($"FileCountCrawlingTask End");
            });

            // TreeView初期化
            var node = new TreeNode("Data");
            node.Tag = new DirectoryDbEntry { Id = -1 };
            node.Nodes.Add("");
            treeView.Nodes.Add(node);
            treeView.BeforeExpand += TreeView_BeforeExpand;
            treeView.NodeMouseClick += TreeView_NodeMouseClick;

            // ListView初期化
            listView.Columns.Add(new ColumnHeader { Text = "#", Width = 60, TextAlign = HorizontalAlignment.Right });
            listView.Columns.Add(new ColumnHeader { Text = Strings.Get("MainForm_ColBackupTime"), Width = 200, TextAlign = HorizontalAlignment.Right });
            listView.Columns.Add(new ColumnHeader { Text = Strings.Get("MainForm_ColSize"), Width = 200, TextAlign = HorizontalAlignment.Right });
            listView.Columns.Add(new ColumnHeader { Text = Strings.Get("MainForm_ColLastWrite"), Width = 200, TextAlign = HorizontalAlignment.Right });
            listView.FullRowSelect = true;
            listView.MouseClick += ListView_MouseClick;
        }

        /// <summary>
        /// ファイルを右クリックした際にコンテキストメニュー表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (listView.SelectedItems.Count == 0) return;

            var item = treeView.SelectedNode?.Tag as FileDbEntry;
            if (item == null) return;
            var menu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem(Strings.Format("MainForm_OpenVersion", item.Name));
            openItem.Click += FileOpenTempMenuItem_Click;
            menu.Items.Add(openItem);
            var menuItem = new ToolStripMenuItem(Strings.Format("MainForm_RestoreFile", item.Name));
            menuItem.Click += FileSubMenuItem_Click;
            menu.Items.Add(menuItem);
            menu.Show(Cursor.Position);
        }

        /// <summary>
        /// 選択した世代を一時ファイルにコピーして既定アプリで開く（プレビュー）
        /// </summary>
        private void FileOpenTempMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var fileDbEntry = treeView.SelectedNode?.Tag as FileDbEntry;
                var attrDbEntry = listView.SelectedItems.Count > 0 ? listView.SelectedItems[0].Tag as AttributeDbEntry : null;
                if (fileDbEntry == null || attrDbEntry == null)
                {
                    MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var backupFileDir = _db.GetFileDir(fileDbEntry.Id);
                var backupFileFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, fileDbEntry.Name), attrDbEntry.BackupTime);
                if (!File.Exists(backupFileFullPath))
                {
                    MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 元のファイル名のまま一意な一時フォルダにコピーして開く
                var tempDir = Path.Combine(Path.GetTempPath(), "FileHistoryClone", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                var tempFile = Path.Combine(tempDir, fileDbEntry.Name);
                File.Copy(backupFileFullPath, tempFile, overwrite: true);
                // 編集しても元に戻らないことを示すため読み取り専用にする
                try { File.SetAttributes(tempFile, FileAttributes.ReadOnly); } catch { }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in FileOpenTempMenuItem_Click(): {ex}");
                MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// ファイルをリストア
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSubMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
            // コピー元ファイル確認
            var fileDbEntry = treeView.SelectedNode?.Tag as FileDbEntry;
            var attrDbEntry = listView.SelectedItems.Count > 0 ? listView.SelectedItems[0].Tag as AttributeDbEntry : null;
            if (fileDbEntry == null || attrDbEntry == null)
            {
                MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 復元先ディレクトリ指定
            var distDir = "";
            using (var fbd = new FolderBrowserDialog()
            {
                Description = Strings.Get("MainForm_SelectRestoreFolder"),
                UseDescriptionForTitle = true,
            })
            {
                if (fbd.ShowDialog() != DialogResult.OK) return;
                distDir = fbd.SelectedPath;
            }

            var backupFileDir = _db.GetFileDir(fileDbEntry.Id);
            var backupFileFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, fileDbEntry.Name), attrDbEntry.BackupTime);
            if (!File.Exists(backupFileFullPath))
            {
                MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // コピー先の上書き確認
            var destFileFullPath = Path.Combine(distDir, fileDbEntry.Name);
            if (File.Exists(destFileFullPath))
            {
                if (DialogResult.Yes != MessageBox.Show(Strings.Get("MainForm_OverwriteFile"), Strings.Get("MainForm_OverwriteTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning)) return;
            }

            // ファイルコピー
            File.Copy(backupFileFullPath, destFileFullPath, overwrite: true);

            // コピー先タイムスタンプ設定
            File.SetCreationTime(destFileFullPath, attrDbEntry.CreationTime);
            File.SetLastWriteTime(destFileFullPath, attrDbEntry.LastWriteTime);
            File.SetLastAccessTime(destFileFullPath, attrDbEntry.LastAccessTime);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in FileSubMenuItem_Click(): {ex}");
                MessageBox.Show(Strings.Get("MainForm_FileNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// ディレクトリを右クリックした際にコンテキストメニュー表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right || !(e.Node.Tag is DirectoryDbEntry)) return;

            treeView.SelectedNode = e.Node;
            var menu = new ContextMenuStrip();
            var menuItem = new ToolStripMenuItem(Strings.Format("MainForm_RestoreDirectory", e.Node.Text));
            menuItem.Click += DirectorySubMenuItem_Click;
            menu.Items.Add(menuItem);
            menu.Show(Cursor.Position);
        }

        /// <summary>
        /// ディレクトリをリストア
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DirectorySubMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
            // コピー元ディレクトリ確認
            var dirDbEntry = treeView.SelectedNode?.Tag as DirectoryDbEntry;
            if (dirDbEntry == null)
            {
                MessageBox.Show(Strings.Get("MainForm_DirectoryNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 復元先ディレクトリ指定
            var distDir = "";
            using (var fbd = new FolderBrowserDialog()
            {
                Description = Strings.Get("MainForm_SelectRestoreFolder"),
                UseDescriptionForTitle = true,
            })
            {
                if (fbd.ShowDialog() != DialogResult.OK) return;
                distDir = fbd.SelectedPath;
            }

            if (Directory.Exists(Path.Combine(distDir, dirDbEntry.Name)))
            {
                if (DialogResult.Yes != MessageBox.Show(Strings.Get("MainForm_OverwriteDirectory"), Strings.Get("MainForm_OverwriteTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning)) return;
                Directory.Delete(Path.Combine(distDir, dirDbEntry.Name), true);
            }
            RestoreDirectory(dirDbEntry, distDir);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception caught in DirectorySubMenuItem_Click(): {ex}");
                MessageBox.Show(Strings.Get("MainForm_DirectoryNotFound"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreDirectory(DirectoryDbEntry dirDbEntry, string distDir)
        {
            // ディレクトリ作成
            Directory.CreateDirectory(Path.Combine(distDir, dirDbEntry.Name));

            // ファイルコピー
            foreach (var file in _db.GetChildFiles(dirDbEntry.Id))
            {
                var attr = _db.GetAttributes(file.Id).OrderBy(m => m.BackupTime).LastOrDefault();
                if (attr == null) continue;
                var backupFileDir = _db.GetFileDir(file.Id);
                var backupFileFullPath = BackupDb.BackupFileName(_settings.DataDir, Path.Combine(backupFileDir, file.Name), attr.BackupTime);
                var destFileFullPath = Path.Combine(distDir, dirDbEntry.Name, file.Name);

                try
                {
                    // ファイルコピー
                    File.Copy(backupFileFullPath, destFileFullPath);
                    // コピー先タイムスタンプ設定
                    File.SetCreationTime(destFileFullPath, attr.CreationTime);
                    File.SetLastWriteTime(destFileFullPath, attr.LastWriteTime);
                    File.SetLastAccessTime(destFileFullPath, attr.LastAccessTime);
                }
                catch (Exception)
                { }
            }

            // ディレクトリコピー
            foreach (var dir in _db.GetChildDirectories(dirDbEntry.Id))
            {
                RestoreDirectory(dir, Path.Combine(distDir, dirDbEntry.Name));
            }
        }

        /// <summary>
        /// ディレクトリ展開
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            e.Node.Nodes.Clear();
            foreach (var ch in _db.GetChildDirectories((e.Node.Tag as DirectoryDbEntry).Id).OrderBy(m => m.Name).ToArray())
            {
                var chNode = new TreeNode(ch.Name);
                chNode.Tag = ch;
                chNode.Nodes.Add("");
                e.Node.Nodes.Add(chNode);
            }
            foreach (var ch in _db.GetChildFiles((e.Node.Tag as DirectoryDbEntry).Id).OrderBy(m => m.Name).ToArray())
            {
                var chNode = new TreeNode(ch.Name);
                chNode.Tag = ch;
                e.Node.Nodes.Add(chNode);
            }
        }

        /// <summary>
        /// クリーンアップ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _logger.LogInformation($"MainForm_FormClosing() called");
            _cts.Cancel();
            Instance = null;
        }

        /// <summary>
        /// ディレクトリ選択
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            listView.Items.Clear();
            if (e.Node.Tag is FileDbEntry)
            {
                var attributes = _db.GetAttributes((e.Node.Tag as FileDbEntry).Id).OrderByDescending(m => m.BackupTime).ToList();
                for (int i = 0; i < attributes.Count; i++)
                {
                    var entry = new ListViewItem(new string[] {
                        (i+1).ToString(),
                        attributes[i].BackupTime.ToString(),
                        attributes[i].Size.ToString("#,0"),
                        attributes[i].LastWriteTime.ToString(),
                    });
                    entry.Tag = attributes[i];
                    listView.Items.Add(entry);
                }
            }
        }

        /// <summary>
        /// バックアップ整理ボタン
        /// </summary>
        private void button_cleanup_Click(object sender, EventArgs e)
        {
            try
            {
                _logger.LogTrace("Enter button_cleanup_Click()");
                new CleanupForm(_settings, _db, _crawler, _loggerFactory).ShowDialog();
                treeView.Nodes[0].Collapse();
                listView.Items.Clear();
            }
            catch(Exception ex)
            {
                _logger.LogError($"Exception caught in button_cleanup_Click(): {ex}");
            }
            finally
            {
                _logger.LogTrace("Leave button_cleanup_Click()");
            }
        }
    }
}
