using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FileHistory
{
    /// <summary>
    /// 全設定を GUI で編集し appsettings.json に書き戻す設定画面。
    /// 一般ユーザーが JSON を手編集しなくて済むようにする。
    /// </summary>
    public class SettingsForm : Form
    {
        readonly string _configPath;

        TableLayoutPanel _tlp;
        FlowLayoutPanel _buttons;
        ToolTip _tips;

        TextBox _backupBaseDir;
        TextBox _includeBox;
        TextBox _excludeBox;
        NumericUpDown _defaultInterval, _idleTimer, _crawlInterval;
        NumericUpDown _maxGenerations, _retentionDays;
        ComboBox _language;

        // IncludeDir は Dir と任意の BackupInterval を保持。
        // テキスト欄では Dir を編集し、Interval は「個別設定」ダイアログで編集する。
        readonly List<IncludeDir> _includeDirs = new List<IncludeDir>();

        // GUI から編集しない項目は読み込んだ値をそのまま書き戻す
        double _retentionScanLoaded = new AppSettingsData().RetentionScanInterval;

        public SettingsForm(string configPath)
        {
            _configPath = configPath;
            BuildUi();
            LoadFromFile();
        }

        void BuildUi()
        {
            Text = Strings.Get("Settings_Title");
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new System.Drawing.Size(560, 400);
            Size = new System.Drawing.Size(600, 680);

            // タスクトレイと同じアプリアイコン(未設定だと WinForms 既定アイコンになる)
            try { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "FileHistoryCloneMainIcon.ico")); }
            catch { try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { } }

            _tips = new ToolTip { AutoPopDelay = 30000, InitialDelay = 400, ReshowDelay = 200 };

            // AutoScroll は画面が低くて全項目が収まらないとき(640x480 等)の保険。
            // 通常は OnLoad で全項目が収まる高さに合わせるためスクロールは出ない。
            var tlp = _tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Padding = new Padding(12),
                AutoScroll = true,
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;
            Label Header(string key)
            {
                var l = new Label { Text = Strings.Get(key), AutoSize = true, Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold), Margin = new Padding(0, 10, 0, 4) };
                tlp.Controls.Add(l, 0, row); tlp.SetColumnSpan(l, 3); row++;
                return l;
            }
            Label FieldLabel(string key)
            {
                var l = new Label { Text = Strings.Get(key), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
                return l;
            }
            // セクション見出し直下に置く 1〜2 行のグレー説明文
            void Hint(string key)
            {
                var l = new Label { Text = Strings.Get(key), AutoSize = true, MaximumSize = new System.Drawing.Size(520, 0), ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) };
                tlp.Controls.Add(l, 0, row); tlp.SetColumnSpan(l, 3); row++;
            }
            void Tip(string key, params Control[] controls)
            {
                var text = Strings.Get(key);
                foreach (var c in controls)
                {
                    _tips.SetToolTip(c, text);
                    // NumericUpDown は内部の子コントロール上ではツールチップが出ないため子にも設定
                    foreach (Control child in c.Controls) _tips.SetToolTip(child, text);
                }
            }

            // --- バックアップ先 ---
            Header("Settings_BackupDest");
            Hint("Settings_BackupDestHint");
            _backupBaseDir = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 2, 0, 2) };
            var browse = new Button { Text = Strings.Get("Settings_Browse"), AutoSize = true, Margin = new Padding(6, 1, 0, 1) };
            browse.Click += (s, e) => { var p = PickFolder(_backupBaseDir.Text); if (p != null) _backupBaseDir.Text = p; };
            tlp.Controls.Add(_backupBaseDir, 0, row); tlp.SetColumnSpan(_backupBaseDir, 2);
            tlp.Controls.Add(browse, 2, row); row++;
            Tip("Settings_Tip_BackupDir", _backupBaseDir);

            // --- 保護対象フォルダ ---
            Header("Settings_ProtectedFolders");
            Hint("Settings_ProtectedFoldersHint");
            // 1行=1フォルダで直接編集できるテキスト欄。末尾には常に空行を維持し、
            // 追加ボタンを使わなくてもそのまま入力できるようにする。
            _includeBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Height = 110,
                Margin = new Padding(0, 2, 0, 2),
                WordWrap = false,
            };
            _includeBox.TextChanged += (s, e) => EnsureTrailingEmptyLine(_includeBox);
            var addInc = new Button { Text = Strings.Get("Settings_Add"), AutoSize = true, Margin = new Padding(6, 1, 0, 1) };
            addInc.Click += (s, e) =>
            {
                var p = PickFolder(null);
                if (p == null) return;
                var lines = IncludeLines();
                if (!lines.Any(l => string.Equals(l, p, StringComparison.OrdinalIgnoreCase)))
                    _includeBox.Text = string.Join("\r\n", lines.Append(p));
            };
            tlp.Controls.Add(_includeBox, 0, row); tlp.SetColumnSpan(_includeBox, 2);
            tlp.Controls.Add(addInc, 2, row); row++;
            Tip("Settings_Tip_IncludeList", _includeBox, addInc);

            // --- 除外パターン ---
            Header("Settings_Exclusions");
            _excludeBox = new TextBox { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 90, Margin = new Padding(0, 2, 0, 2), WordWrap = false };
            tlp.Controls.Add(_excludeBox, 0, row); tlp.SetColumnSpan(_excludeBox, 3); row++;
            var exHint = new Label { Text = Strings.Get("Settings_ExclusionsHint"), AutoSize = true, ForeColor = System.Drawing.SystemColors.GrayText, Margin = new Padding(0, 0, 0, 4) };
            tlp.Controls.Add(exHint, 0, row); tlp.SetColumnSpan(exHint, 3); row++;
            Tip("Settings_Tip_Exclude", _excludeBox, exHint);

            // --- タイミング ---
            Header("Settings_Timing");
            Hint("Settings_TimingHint");
            _defaultInterval = MakeNumeric(0, int.MaxValue);
            _idleTimer = MakeNumeric(0, int.MaxValue);
            _crawlInterval = MakeNumeric(0, int.MaxValue);
            // 再バックアップ間隔の行だけ、フォルダごとの個別設定ボタンを右端に置く
            var perFolder = new Button { Text = Strings.Get("Settings_PerFolder"), AutoSize = true, Margin = new Padding(6, 1, 0, 1) };
            perFolder.Click += PerFolder_Click;
            AddNumericRow(tlp, ref row, "Settings_DefaultInterval", _defaultInterval, Tip, "Settings_Tip_DefaultInterval", perFolder);
            Tip("Settings_Tip_PerFolder", perFolder);
            AddNumericRow(tlp, ref row, "Settings_IdleTimer", _idleTimer, Tip, "Settings_Tip_IdleTimer");
            AddNumericRow(tlp, ref row, "Settings_CrawlInterval", _crawlInterval, Tip, "Settings_Tip_CrawlInterval");

            // --- 保持ポリシー ---
            // (適用間隔 RetentionScanInterval は内部動作の詳細なので GUI には出さない。
            //  appsettings.json の値はそのまま温存される)
            Header("Settings_Retention");
            Hint("Settings_RetentionHint");
            _maxGenerations = MakeNumeric(0, int.MaxValue);
            _retentionDays = MakeNumeric(0, int.MaxValue);
            AddNumericRow(tlp, ref row, "Settings_MaxGenerations", _maxGenerations, Tip, "Settings_Tip_MaxGenerations");
            AddNumericRow(tlp, ref row, "Settings_RetentionDays", _retentionDays, Tip, "Settings_Tip_RetentionDays");

            // --- 言語 ---
            Header("Settings_Language");
            _language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left, Width = 200, Margin = new Padding(0, 2, 0, 2) };
            _language.Items.Add(Strings.Get("Settings_LangAuto")); // index 0 -> ""
            _language.Items.Add("English");                        // index 1 -> "en"
            _language.Items.Add("日本語");                          // index 2 -> "ja"
            var langLabel = FieldLabel("Settings_UiLanguage");
            tlp.Controls.Add(langLabel, 0, row);
            tlp.Controls.Add(_language, 1, row); row++;
            Tip("Settings_Tip_Language", langLabel, _language);

            // TableLayoutPanel の AutoScroll は Padding 分をスクロール範囲に含めず
            // 最下行が切れて見えなくなるため、スペーサー行で範囲を底上げする
            var spacer = new Panel { Height = 24, Width = 1, Margin = new Padding(0) };
            tlp.Controls.Add(spacer, 0, row); row++;

            // --- ボタン ---
            var buttons = _buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8), Height = 48 };
            var save = new Button { Text = Strings.Get("Settings_Save"), AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = Strings.Get("Settings_Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            save.Click += Save_Click;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            AcceptButton = save; CancelButton = cancel;

            Controls.Add(tlp);
            Controls.Add(buttons);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // 全項目がスクロールなしで収まる高さに合わせる。
            // 画面(作業領域)より高くなる場合のみ収まる高さに切り詰め、AutoScroll に任せる。
            int chrome = Height - ClientSize.Height;
            int needed = _tlp.PreferredSize.Height + _buttons.Height + chrome + 8;
            var wa = Screen.FromControl(this).WorkingArea;
            Height = Math.Min(needed, wa.Height);
            Width = Math.Min(Width, wa.Width);
            // 高さが変わっても画面中央に置き直す
            Top = wa.Top + Math.Max(0, (wa.Height - Height) / 2);
            Left = Math.Max(wa.Left, Left);
        }

        NumericUpDown MakeNumeric(int min, int max)
            => new NumericUpDown { Minimum = min, Maximum = max, Width = 140, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 2), ThousandsSeparator = true };

        void AddNumericRow(TableLayoutPanel tlp, ref int row, string labelKey, NumericUpDown num, Action<string, Control[]> tip, string tipKey, Control extra = null)
        {
            var l = new Label { Text = Strings.Get(labelKey), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
            tlp.Controls.Add(l, 0, row);
            tlp.Controls.Add(num, 1, row);
            if (extra != null) tlp.Controls.Add(extra, 2, row);
            row++;
            tip(tipKey, new Control[] { l, num });
        }

        /// <summary>末尾に必ず空行を1つ置く(最下行にそのまま入力できるようにする)</summary>
        static void EnsureTrailingEmptyLine(TextBox box)
        {
            if (box.Text.Length == 0 || box.Text.EndsWith("\r\n")) return;
            var sel = box.SelectionStart;
            box.AppendText("\r\n");   // AppendText はキャレットを末尾に移すため戻す
            box.SelectionStart = sel;
        }

        /// <summary>保護対象テキスト欄の有効行(トリム済み・空行除去)</summary>
        List<string> IncludeLines()
            => _includeBox.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        /// <summary>
        /// テキスト欄の内容を _includeDirs に反映する。
        /// 既存エントリと同じパスは BackupInterval(個別間隔)を引き継ぐ。
        /// </summary>
        void SyncIncludeDirsFromText()
        {
            var result = new List<IncludeDir>();
            foreach (var line in IncludeLines())
            {
                if (result.Any(d => string.Equals(d.Dir, line, StringComparison.OrdinalIgnoreCase))) continue;
                var existing = _includeDirs.FirstOrDefault(d => string.Equals((d.Dir ?? "").Trim(), line, StringComparison.OrdinalIgnoreCase));
                result.Add(new IncludeDir { Dir = line, BackupInterval = existing?.BackupInterval });
            }
            _includeDirs.Clear();
            _includeDirs.AddRange(result);
        }

        void PerFolder_Click(object sender, EventArgs e)
        {
            SyncIncludeDirsFromText();
            if (_includeDirs.Count == 0)
            {
                MessageBox.Show(Strings.Get("Settings_NeedIncludeDir"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var dlg = new PerFolderIntervalForm(_includeDirs, Icon);
            dlg.ShowDialog(this);
        }

        static string PickFolder(string initial)
        {
            using var fbd = new FolderBrowserDialog { Description = Strings.Get("Settings_SelectFolder"), UseDescriptionForTitle = true };
            if (!string.IsNullOrEmpty(initial))
            {
                try { var e = Environment.ExpandEnvironmentVariables(initial); if (Directory.Exists(e)) fbd.SelectedPath = e; } catch { }
            }
            return fbd.ShowDialog() == DialogResult.OK ? fbd.SelectedPath : null;
        }

        // ---- 読み込み ----
        void LoadFromFile()
        {
            try
            {
                var d = AppSettingsFile.Load(_configPath);

                _backupBaseDir.Text = d.BackupBaseDir;
                _defaultInterval.Value = ClampToNumeric(_defaultInterval, d.DefaultBackupInterval);
                _idleTimer.Value = ClampToNumeric(_idleTimer, d.CrawlingIdleTimer);
                _crawlInterval.Value = ClampToNumeric(_crawlInterval, d.CrawlingInterval);
                _maxGenerations.Value = ClampToNumeric(_maxGenerations, d.MaxGenerations);
                _retentionDays.Value = ClampToNumeric(_retentionDays, d.RetentionDays);
                _retentionScanLoaded = d.RetentionScanInterval;

                _language.SelectedIndex = (d.Language ?? "").Equals("en", StringComparison.OrdinalIgnoreCase) ? 1
                    : (d.Language ?? "").Equals("ja", StringComparison.OrdinalIgnoreCase) ? 2 : 0;

                _includeDirs.Clear();
                _includeDirs.AddRange(d.IncludeDirs);
                _includeBox.Text = string.Join("\r\n", _includeDirs.Select(x => x.Dir));

                _excludeBox.Text = string.Join("\r\n", d.ExcludeDirs);
            }
            catch { /* 壊れた設定でも UI は開く(既定値のまま) */ }
        }

        static decimal ClampToNumeric(NumericUpDown n, double value)
        { var d = (decimal)Math.Max(0, value); return d < n.Minimum ? n.Minimum : d > n.Maximum ? n.Maximum : d; }

        // ---- 保存 ----
        void Save_Click(object sender, EventArgs e)
        {
            SyncIncludeDirsFromText();

            // 検証: バックアップ先と保護対象は最低限必要
            if (string.IsNullOrWhiteSpace(_backupBaseDir.Text))
            {
                MessageBox.Show(Strings.Get("Settings_NeedBackupDir"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            if (_includeDirs.Count == 0)
            {
                MessageBox.Show(Strings.Get("Settings_NeedIncludeDir"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }

            try
            {
                var d = new AppSettingsData
                {
                    BackupBaseDir = _backupBaseDir.Text.Trim(),
                    DefaultBackupInterval = (double)_defaultInterval.Value,
                    CrawlingIdleTimer = (double)_idleTimer.Value,
                    CrawlingInterval = (double)_crawlInterval.Value,
                    MaxGenerations = (int)_maxGenerations.Value,
                    RetentionDays = (double)_retentionDays.Value,
                    RetentionScanInterval = _retentionScanLoaded,
                    Language = _language.SelectedIndex == 1 ? "en" : _language.SelectedIndex == 2 ? "ja" : "",
                    IncludeDirs = _includeDirs.ToList(),
                    ExcludeDirs = _excludeBox.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList(),
                };
                AppSettingsFile.Save(_configPath, d);
                // DialogResult.OK は Save ボタンに設定済み → 呼び出し側で再起動を促す
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }
    }

    /// <summary>
    /// フォルダごとのバックアップ間隔(IncludeDir.BackupInterval)を編集するダイアログ。
    /// 空欄なら既定値(DefaultBackupInterval)を使う。
    /// </summary>
    class PerFolderIntervalForm : Form
    {
        readonly List<IncludeDir> _dirs;
        readonly DataGridView _grid;

        public PerFolderIntervalForm(List<IncludeDir> dirs, System.Drawing.Icon icon)
        {
            _dirs = dirs;

            Text = Strings.Get("Settings_PerFolderTitle");
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(480, 240);
            Size = new System.Drawing.Size(560, 320);
            if (icon != null) Icon = icon;

            var hint = new Label
            {
                Text = Strings.Get("Settings_PerFolderHint"),
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 48,    // 2行分(折り返し表示)
                ForeColor = System.Drawing.SystemColors.GrayText,
                Padding = new Padding(10, 6, 10, 0),
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                BackgroundColor = System.Drawing.SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
            };
            var colDir = new DataGridViewTextBoxColumn
            {
                HeaderText = Strings.Get("Settings_PerFolderColFolder"),
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            var colInterval = new DataGridViewTextBoxColumn
            {
                HeaderText = Strings.Get("Settings_PerFolderColInterval"),
                Width = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            _grid.Columns.Add(colDir);
            _grid.Columns.Add(colInterval);
            foreach (var d in dirs)
                _grid.Rows.Add(d.Dir, d.BackupInterval?.ToString() ?? "");

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 10, 8), Height = 46 };
            var ok = new Button { Text = Strings.Get("Settings_OK"), AutoSize = true };
            var cancel = new Button { Text = Strings.Get("Settings_Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            ok.Click += Ok_Click;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;

            Controls.Add(_grid);
            Controls.Add(hint);
            Controls.Add(buttons);
        }

        void Ok_Click(object sender, EventArgs e)
        {
            _grid.EndEdit();
            var values = new double?[_dirs.Count];
            for (int i = 0; i < _dirs.Count; i++)
            {
                var text = (_grid.Rows[i].Cells[1].Value?.ToString() ?? "").Trim();
                if (text.Length == 0) { values[i] = null; continue; }
                if (!double.TryParse(text, out var v) || v < 0)
                {
                    MessageBox.Show(Strings.Get("Settings_PerFolderInvalid"), Strings.Get("Common_Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _grid.CurrentCell = _grid.Rows[i].Cells[1];
                    return;
                }
                values[i] = v;
            }
            for (int i = 0; i < _dirs.Count; i++) _dirs[i].BackupInterval = values[i];
            DialogResult = DialogResult.OK;
        }
    }
}
