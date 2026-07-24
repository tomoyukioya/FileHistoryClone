using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

// UI 検証用スクリーンショットハーネス。
// 全フォームをオフスクリーンで描画して PNG に保存する(画面にはほぼ表示されない)。
//
// 使い方:
//   dotnet run --project tools/FormPreview -- ja-JP ja
//   dotnet run --project tools/FormPreview -- en-US en
// 第1引数 = UI カルチャ、第2引数 = 出力 PNG のプレフィックス。
// 出力: <prefix>_settings.png / <prefix>_settings_640top.png / <prefix>_settings_640btm.png
//       <prefix>_main.png / <prefix>_perfolder.png / <prefix>_cleanup.png
class Preview
{
    static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Show();
        // 描画完了を確実に待つ(1回の DoEvents では不足することがある)
        for (int i = 0; i < 10; i++) { Thread.Sleep(50); Application.DoEvents(); }
    }

    static void Capture(Form form, string path)
    {
        Application.DoEvents();
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(path);
        Console.WriteLine($"Saved: {path} ({form.Width}x{form.Height})");
    }

    [STAThread]
    static void Main(string[] args)
    {
        var culture = args.Length > 0 ? args[0] : "ja-JP";
        var prefix = args.Length > 1 ? args[1] : "shot";
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(culture);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // --- SettingsForm 通常 (存在しない設定パス = 既定値表示) ---
        var sf = new FileHistory.SettingsForm(@"nonexistent-config.json");
        ShowOffscreen(sf);
        Console.WriteLine($"Settings form auto-sized: {sf.Size}");
        Capture(sf, $"{prefix}_settings.png");

        // --- 640x480 シミュレーション (480 からタスクバー分を引いた 440 に制限) ---
        sf.MinimumSize = new Size(400, 300);
        sf.Size = new Size(600, 440);
        Application.DoEvents();
        Capture(sf, $"{prefix}_settings_640top.png");
        var tlp = sf.Controls.OfType<TableLayoutPanel>().First();
        tlp.AutoScrollPosition = new Point(0, 100000);  // 最下部へスクロール
        Application.DoEvents();
        Capture(sf, $"{prefix}_settings_640btm.png");   // 最下行(UI言語)が見えること
        sf.Close();

        // --- MainForm (メニュー・タイトルのバージョン表示確認) ---
        var mf = new FileHistory.MainForm(new FileHistory.Settings(), null,
            LoggerFactory.Create(b => { }), null);
        ShowOffscreen(mf);
        Console.WriteLine($"MainForm title: {mf.Text}");
        Capture(mf, $"{prefix}_main.png");
        mf.Close();

        // --- 個別設定ダイアログ (internal クラスのためリフレクションで生成) ---
        var asm = typeof(FileHistory.SettingsForm).Assembly;
        var t = asm.GetType("FileHistory.PerFolderIntervalForm");
        var dirs = new System.Collections.Generic.List<FileHistory.IncludeDir>
        {
            new FileHistory.IncludeDir { Dir = @"C:\Users\me\Documents" },
            new FileHistory.IncludeDir { Dir = @"D:\Projects", BackupInterval = 600 },
        };
        var dlg = (Form)Activator.CreateInstance(t, dirs, null);
        ShowOffscreen(dlg);
        Capture(dlg, $"{prefix}_perfolder.png");
        dlg.Close();

        // --- CleanupForm ---
        var cf = new FileHistory.CleanupForm(new FileHistory.Settings(), null, null,
            LoggerFactory.Create(b => { }));
        ShowOffscreen(cf);
        Capture(cf, $"{prefix}_cleanup.png");
        cf.Close();
    }
}
