using FileHistory;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FileHistory.Tests
{
    [TestClass()]
    public class AppSettingsFileTests
    {
        string _path = "";

        [TestInitialize()]
        public void Init()
        {
            _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        }

        [TestCleanup()]
        public void Cleanup()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [TestMethod()]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var d = AppSettingsFile.Load(_path);
            Assert.AreEqual(3600, d.DefaultBackupInterval);
            Assert.AreEqual(0, d.IncludeDirs.Count);
            Assert.AreEqual(0, d.ExcludeDirs.Count);
        }

        [TestMethod()]
        public void SaveThenLoad_RoundTrips()
        {
            var d = new AppSettingsData
            {
                BackupBaseDir = @"D:\Backup",
                DefaultBackupInterval = 5,
                CrawlingIdleTimer = 10,
                CrawlingInterval = 42,
                MaxGenerations = 7,
                RetentionDays = 30,
                RetentionScanInterval = 111,
                Language = "ja",
            };
            d.IncludeDirs.Add(new IncludeDir { Dir = @"C:\Docs" });
            d.IncludeDirs.Add(new IncludeDir { Dir = @"C:\Pics", BackupInterval = 900 });
            d.ExcludeDirs.Add(".git");
            d.ExcludeDirs.Add("*.tmp");

            AppSettingsFile.Save(_path, d);
            var r = AppSettingsFile.Load(_path);

            Assert.AreEqual(@"D:\Backup", r.BackupBaseDir);
            Assert.AreEqual(5, r.DefaultBackupInterval);
            Assert.AreEqual(10, r.CrawlingIdleTimer);
            Assert.AreEqual(42, r.CrawlingInterval);
            Assert.AreEqual(7, r.MaxGenerations);
            Assert.AreEqual(30, r.RetentionDays);
            Assert.AreEqual(111, r.RetentionScanInterval);
            Assert.AreEqual("ja", r.Language);

            Assert.AreEqual(2, r.IncludeDirs.Count);
            Assert.AreEqual(@"C:\Docs", r.IncludeDirs[0].Dir);
            Assert.IsNull(r.IncludeDirs[0].BackupInterval);
            Assert.AreEqual(@"C:\Pics", r.IncludeDirs[1].Dir);
            Assert.AreEqual(900, r.IncludeDirs[1].BackupInterval);

            CollectionAssert.AreEqual(new[] { ".git", "*.tmp" }, r.ExcludeDirs.ToArray());
        }

        [TestMethod()]
        public void Save_PreservesLoggingSection()
        {
            // Logging セクションを持つ既存ファイルを用意
            File.WriteAllText(_path, "{ \"Settings\": { \"BackupBaseDir\": \"X\" }, \"Logging\": { \"File\": { \"Path\": \"log.txt\" } } }");

            var d = AppSettingsFile.Load(_path);
            d.BackupBaseDir = @"C:\New";
            AppSettingsFile.Save(_path, d);

            var text = File.ReadAllText(_path);
            StringAssert.Contains(text, "Logging");
            StringAssert.Contains(text, "log.txt");
            StringAssert.Contains(text, @"C:\\New");
        }

        [TestMethod()]
        public void Load_ToleratesCommentsAndTrailingCommas()
        {
            File.WriteAllText(_path,
                "{ \"Settings\": {\n" +
                "  \"BackupBaseDir\": \"C:\\\\B\", // comment\n" +
                "  \"DefaultBackupInterval\": 12,\n" +
                "} }");
            var d = AppSettingsFile.Load(_path);
            Assert.AreEqual(@"C:\B", d.BackupBaseDir);
            Assert.AreEqual(12, d.DefaultBackupInterval);
        }

        [TestMethod()]
        public void Load_CorruptFile_ReturnsDefaults()
        {
            File.WriteAllText(_path, "{ not valid json \\U oops");
            var d = AppSettingsFile.Load(_path);
            Assert.AreEqual(3600, d.DefaultBackupInterval);
            Assert.AreEqual(0, d.IncludeDirs.Count);
        }

        [TestMethod()]
        public void Save_OverCorruptFile_WritesValidJson()
        {
            File.WriteAllText(_path, "{ this is : broken \\F json ,,, ");
            var d = new AppSettingsData { BackupBaseDir = @"C:\Ok", DefaultBackupInterval = 9 };
            AppSettingsFile.Save(_path, d);   // must not throw

            var r = AppSettingsFile.Load(_path);
            Assert.AreEqual(@"C:\Ok", r.BackupBaseDir);
            Assert.AreEqual(9, r.DefaultBackupInterval);
        }

        [TestMethod()]
        public void Load_ShippedV100File_UpgradesCleanly()
        {
            // v1.0.0 が同梱していた appsettings.json(コメント付き・現行と同一内容)を
            // そのまま読み書きできること = 上書きアップグレードで設定が引き継がれること
            var shipped = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            Assert.IsTrue(File.Exists(shipped), "appsettings.json not found in test output");
            File.Copy(shipped, _path, overwrite: true);

            var d = AppSettingsFile.Load(_path);
            Assert.AreEqual(@"%USERPROFILE%\FileHistoryCloneBackup", d.BackupBaseDir);
            Assert.AreEqual(3600, d.DefaultBackupInterval);
            Assert.AreEqual(1, d.IncludeDirs.Count);
            Assert.AreEqual(@"%USERPROFILE%\Documents", d.IncludeDirs[0].Dir);
            Assert.AreEqual(4, d.ExcludeDirs.Count);
            Assert.AreEqual(86400, d.RetentionScanInterval);

            // GUI で一部だけ変えて保存しても、Logging セクションと
            // GUI にない設定(RetentionScanInterval)が失われないこと
            d.MaxGenerations = 5;
            AppSettingsFile.Save(_path, d);

            var r = AppSettingsFile.Load(_path);
            Assert.AreEqual(5, r.MaxGenerations);
            Assert.AreEqual(86400, r.RetentionScanInterval);
            var text = File.ReadAllText(_path);
            StringAssert.Contains(text, "Logging");
            StringAssert.Contains(text, "FileSizeLimitBytes");
        }

        [TestMethod()]
        public void SavedFile_BindsBackToSettings()
        {
            // GUI が保存した JSON が、アプリの設定バインドで正しく読めること
            var d = new AppSettingsData { BackupBaseDir = @"E:\B", DefaultBackupInterval = 77, Language = "en" };
            d.IncludeDirs.Add(new IncludeDir { Dir = @"C:\A", BackupInterval = 50 });
            d.ExcludeDirs.Add("node_modules");
            AppSettingsFile.Save(_path, d);

            var settings = new Settings();
            new ConfigurationBuilder().AddJsonFile(_path).Build().GetSection("Settings").Bind(settings);

            Assert.AreEqual(@"E:\B", settings.BackupBaseDir);
            Assert.AreEqual(77, settings.DefaultBackupInterval);
            Assert.AreEqual("en", settings.Language);
            Assert.AreEqual(1, settings.IncludeDirs.Count);
            Assert.AreEqual(@"C:\A", settings.IncludeDirs[0].Dir);
            Assert.AreEqual(50, settings.IncludeDirs[0].BackupInterval);
            Assert.IsTrue(settings.IsExcluded(@"C:\proj\node_modules\x"));
        }
    }
}
