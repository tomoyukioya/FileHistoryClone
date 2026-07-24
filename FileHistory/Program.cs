using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileHistory
{
    internal static class Program
    {
        static Crawler _crawler;
        static IBackupScheduler _backupScheduler;
        static DirectoryWatcher _directoryWatcher;
        static IBackupDb _backupDb;
        static RetentionWorker _retentionWorker;
        static readonly bool DEBUG_MAIN_FORM = false;
        static NotifyIcon _icon;
        static IdleTimeWatcher _idleTimeWatcher;
        static ILogger logger;

        // Windows起動時の自動実行 (HKCU\...\Run)
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "FileHistoryClone";

        static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) != null;
        }

        static void SetAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enable)
                key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        // appsettings.json は実行ファイルと同じフォルダに置く
        internal static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        /// <summary>
        /// 現在のアプリを終了し、少し待ってから起動し直す(設定変更の反映用)。
        /// 遅延を挟むのは多重起動防止 Mutex の解放を待つため。
        /// </summary>
        static void RestartApp()
        {
            try
            {
                var exe = Environment.ProcessPath;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
                    $"/c ping -n 3 127.0.0.1 >nul & start \"\" \"{exe}\"") { CreateNoWindow = true, UseShellExecute = false });
            }
            catch (Exception ex) { logger?.LogError("Exception caught in RestartApp(): {ex}", ex); }
            Application.Exit();
        }

        /// <summary>
        /// 設定 GUI を開き、保存されたら再起動を促す。(メイン画面のメニューから呼ばれる)
        /// </summary>
        internal static void OpenSettings()
        {
            try
            {
                using var sf = new SettingsForm(ConfigPath);
                if (sf.ShowDialog() == DialogResult.OK)
                {
                    if (MessageBox.Show(Strings.Get("Settings_RestartPrompt"), "FileHistoryClone",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        RestartApp();
                }
            }
            catch (Exception ex) { logger?.LogError("Exception caught in OpenSettings(): {ex}", ex); }
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 多重起動防止
            using var mutex = new Mutex(true, @"Local\FileHistoryClone", out var createdNew);
            if (!createdNew)
            {
                MessageBox.Show(Strings.Get("Tray_AlreadyRunning"), "FileHistoryClone",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Winform
            Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ApplicationExit += (s, e) =>
            {
                _crawler?.Dispose();
                _directoryWatcher?.Dispose();
                _backupScheduler?.Dispose();
                _retentionWorker?.Dispose();
                _backupDb?.Dispose();
                _idleTimeWatcher?.Dispose();
                _icon?.Dispose();
            };

            while (true)
            {
                ServiceProvider serviceProvider = default;

                try
                {
                    // appsettings.json読み込み
                    // (スタートアップ起動などカレントディレクトリが実行ファイルと異なる場合に備えて実行ファイル基準で解決)
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false);
                    IConfiguration configuration = builder.Build();

                    // 設定バインド (全サービスで単一のSettingsインスタンスを共有)
                    var settings = new Settings();
                    configuration.GetSection("Settings").Bind(settings);

                    // UI言語設定 (未指定ならOSの言語に従う)
                    if (!string.IsNullOrWhiteSpace(settings.Language))
                    {
                        var culture = new CultureInfo(settings.Language);
                        CultureInfo.DefaultThreadCurrentUICulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;
                    }

                    // Service
                    var services = new ServiceCollection();
                    services.AddSingleton(configuration);
                    services.AddSingleton(settings);
                    services.AddTransient<MainForm>();
                    services.AddSingleton<Crawler>();
                    services.AddSingleton<DirectoryWatcher>();
                    services.AddSingleton<IBackupScheduler, BackupScheduler>();
                    services.AddSingleton<IBackupDb, BackupDb>();
                    services.AddSingleton<RetentionWorker>();
                    services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(loggingBuilder =>
                    {
                        var loggingSection = configuration.GetSection("Logging");
                        // MinLevel取得
                        var minLevelText = loggingSection.GetSection("File").GetValue<string>("MinLevel");
                        if (!Enum.TryParse<LogLevel>(minLevelText, ignoreCase: true, out var minLevel))
                            minLevel = LogLevel.Debug;
                        loggingBuilder.AddFile(loggingSection, fileLoggerOpts =>
                        {
                            fileLoggerOpts.MinLevel = minLevel;
                            // ログパスでも %USERPROFILE% などの環境変数を使えるようにする
                            fileLoggerOpts.FormatLogFileName = fname => Environment.ExpandEnvironmentVariables(fname);
                        });
                        loggingBuilder.AddDebug();
                        loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                    }));
                    services.AddSingleton<IdleTimeWatcher>();
                    serviceProvider = services.BuildServiceProvider();

                    // logging
                    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    logger = loggerFactory.CreateLogger("Main");
                    logger?.LogInformation("Program Start:");
                }
                catch (Exception ex)
                {
                    logger?.LogError("Exception caught in Main(), exit: {ex}", ex);
                    break;
                }

                try
                {
                    // タスクトレイ
                    _icon = new NotifyIcon
                    {
                        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "FileHistoryCloneMainIcon.ico")),
                        Visible = true,
                        Text = "FileHistoryClone"
                    };
                    var menu = new ContextMenuStrip();
                    var menuOpen = new ToolStripMenuItem { Text = Strings.Get("Tray_Open") };
                    void OpenMainForm()
                    {
                        try
                        {
                            logger?.LogInformation($"Menu-Open by user operation.");
                            if (MainForm.Instance != null)
                            {
                                logger?.LogInformation($"Activate MainForm.");
                                MainForm.Instance.Activate();
                            }
                            else
                            {
                                logger?.LogInformation($"Create new MainForm instance.");
                                serviceProvider.GetRequiredService<MainForm>().Show();
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError("Exception caught in menuOpen_Click(): {ex}", ex);
                        }
                    }
                    menuOpen.Click += (s, e) => OpenMainForm();
                    var menuAutoStart = new ToolStripMenuItem
                    {
                        Text = Strings.Get("Tray_AutoStart"),
                        CheckOnClick = true,
                        Checked = IsAutoStartEnabled(),
                    };
                    menuAutoStart.CheckedChanged += (s, e) =>
                    {
                        try
                        {
                            logger?.LogInformation($"Menu-AutoStart set to {menuAutoStart.Checked} by user operation.");
                            SetAutoStart(menuAutoStart.Checked);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError("Exception caught in menuAutoStart_CheckedChanged(): {ex}", ex);
                        }
                    };
                    var menuExit = new ToolStripMenuItem { Text = Strings.Get("Tray_Exit") };
                    menuExit.Click += (s, e) =>
                    {
                        logger?.LogInformation($"Menu-Close by user operation.");
                        Application.Exit();
                    };
                    menu.Items.Add(menuOpen);
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(menuAutoStart);
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(menuExit);
                    _icon.ContextMenuStrip = menu;
                    _icon.DoubleClick += (s, e) => OpenMainForm();

                    // Application起動
                    // MainForm作成
                    if (DEBUG_MAIN_FORM)
                    {
                        var mainForm = serviceProvider.GetRequiredService<MainForm>();
                        Application.Run(mainForm);
                    }
                    else
                    {
                        var settings = serviceProvider.GetRequiredService<Settings>();

                        // 初回起動: 設定 GUI でバックアップ先・保護フォルダを指定してもらう
                        var firstRunMarker = Path.Combine(settings.ConfigDir, ".firstrun");
                        if (!File.Exists(firstRunMarker))
                        {
                            try { Directory.CreateDirectory(settings.ConfigDir); File.WriteAllText(firstRunMarker, DateTime.Now.ToString("o")); } catch { }
                            // マーカーがなくてもバックアップ実績(カタログDB)があれば
                            // 旧バージョンからのアップグレードなので、初回セットアップは出さない
                            if (!File.Exists(settings.BackupDb))
                            {
                                using var sf = new SettingsForm(ConfigPath);
                                if (sf.ShowDialog() == DialogResult.OK)
                                {
                                    RestartApp();   // 新しい設定で起動し直す
                                    break;
                                }
                                // キャンセル時は既定設定のまま起動を続ける
                            }
                        }

                        _backupDb = serviceProvider.GetRequiredService<IBackupDb>();
                        _backupScheduler = serviceProvider.GetRequiredService<IBackupScheduler>();
                        _directoryWatcher = serviceProvider.GetRequiredService<DirectoryWatcher>();
                        _crawler = serviceProvider.GetRequiredService<Crawler>();
                        _retentionWorker = serviceProvider.GetRequiredService<RetentionWorker>();
                        _idleTimeWatcher = serviceProvider.GetRequiredService<IdleTimeWatcher>();
                        Application.Run();
                    }

                    // Application.Exit()による正常終了
                    logger?.LogInformation("Program Exit.");
                    break;
                }
                catch (Exception ex)
                {
                    logger?.LogError("Exception caught in Main(), restart: {ex}", ex);

                    // ゾンビトレイアイコン防止: 残っている_iconを明示的に破棄
                    try { _icon?.Dispose(); } catch { }
                    _icon = null;

                    // 起動直後に連続失敗するケース（例: IncludeDirsの設定ミス）で
                    // CPUを食い潰さないよう少し待機してから再試行する
                    Thread.Sleep(10000);
                }
            }
        }
    }
}
