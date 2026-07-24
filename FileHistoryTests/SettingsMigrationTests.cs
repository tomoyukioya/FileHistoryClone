using FileHistory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace FileHistory.Tests
{
    /// <summary>
    /// v1.1 のバックアップフォルダ名変更(Data→BackupFiles、Configuration→Database)と
    /// 旧フォルダの自動移行の試験
    /// </summary>
    [TestClass()]
    public class SettingsMigrationTests
    {
        string _baseDir = "";
        string MachineBase => Path.Combine(_baseDir, Environment.UserName, Environment.MachineName);

        [TestInitialize()]
        public void Initialize()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_baseDir);
        }

        [TestCleanup()]
        public void Cleanup()
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, true);
        }

        [TestMethod()]
        public void LegacyDirs_AreRenamed_OnFirstAccess()
        {
            Directory.CreateDirectory(Path.Combine(MachineBase, "Data"));
            File.WriteAllText(Path.Combine(MachineBase, "Data", "a.txt"), "x");
            Directory.CreateDirectory(Path.Combine(MachineBase, "Configuration"));
            File.WriteAllText(Path.Combine(MachineBase, "Configuration", "Catalog.db"), "x");

            var settings = new Settings { BackupBaseDir = _baseDir };

            Assert.AreEqual(Path.Combine(MachineBase, "BackupFiles"), settings.DataDir);
            Assert.AreEqual(Path.Combine(MachineBase, "Database"), settings.ConfigDir);
            Assert.IsTrue(File.Exists(Path.Combine(MachineBase, "BackupFiles", "a.txt")), "旧 Data の中身が移行されること");
            Assert.IsTrue(File.Exists(Path.Combine(MachineBase, "Database", "Catalog.db")), "旧 Configuration の中身が移行されること");
            Assert.IsFalse(Directory.Exists(Path.Combine(MachineBase, "Data")), "旧 Data フォルダが残らないこと");
            Assert.IsFalse(Directory.Exists(Path.Combine(MachineBase, "Configuration")), "旧 Configuration フォルダが残らないこと");
        }

        [TestMethod()]
        public void AccessingConfigDirAlone_MigratesDataDirToo()
        {
            // ConfigDir は起動直後に必ずアクセスされるが、DataDir は最初のバックアップまで
            // アクセスされない。片方のアクセスで両方移行されること(v1.1 アップグレード直後の一括移行)
            Directory.CreateDirectory(Path.Combine(MachineBase, "Data"));
            File.WriteAllText(Path.Combine(MachineBase, "Data", "a.txt"), "x");
            Directory.CreateDirectory(Path.Combine(MachineBase, "Configuration"));

            var settings = new Settings { BackupBaseDir = _baseDir };
            _ = settings.ConfigDir;   // ConfigDir だけアクセス

            Assert.IsTrue(Directory.Exists(Path.Combine(MachineBase, "BackupFiles")), "Data も移行されること");
            Assert.IsFalse(Directory.Exists(Path.Combine(MachineBase, "Data")));
        }

        [TestMethod()]
        public void FreshInstall_UsesNewNames()
        {
            var settings = new Settings { BackupBaseDir = _baseDir };

            Assert.AreEqual(Path.Combine(MachineBase, "BackupFiles"), settings.DataDir);
            Assert.AreEqual(Path.Combine(MachineBase, "Database"), settings.ConfigDir);
        }

        [TestMethod()]
        public void WhenBothExist_NewDirWins_AndLegacyIsUntouched()
        {
            Directory.CreateDirectory(Path.Combine(MachineBase, "Data"));
            File.WriteAllText(Path.Combine(MachineBase, "Data", "legacy.txt"), "x");
            Directory.CreateDirectory(Path.Combine(MachineBase, "BackupFiles"));
            File.WriteAllText(Path.Combine(MachineBase, "BackupFiles", "new.txt"), "x");

            var settings = new Settings { BackupBaseDir = _baseDir };

            Assert.AreEqual(Path.Combine(MachineBase, "BackupFiles"), settings.DataDir);
            Assert.IsTrue(File.Exists(Path.Combine(MachineBase, "Data", "legacy.txt")), "両方あるときは旧フォルダに触らないこと");
            Assert.IsTrue(File.Exists(Path.Combine(MachineBase, "BackupFiles", "new.txt")));
        }
    }
}
