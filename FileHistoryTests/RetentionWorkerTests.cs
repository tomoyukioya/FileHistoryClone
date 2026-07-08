using FileHistory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace FileHistory.Tests
{
    [TestClass()]
    public class RetentionWorkerTests
    {
        string _baseDir = "";
        BackupDb _db = null!;

        [TestInitialize()]
        public void Initialize()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_baseDir);
        }

        [TestCleanup()]
        public void Cleanup()
        {
            _db?.Dispose();
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, true);
        }

        Settings MakeSettings(int maxGenerations, double retentionDays)
            => new Settings
            {
                BackupBaseDir = _baseDir,
                MaxGenerations = maxGenerations,
                RetentionDays = retentionDays,
            };

        /// <summary>
        /// 指定した各バックアップ時刻について、DBの属性と実体のバックアップファイルを作成する
        /// </summary>
        void SeedGenerations(Settings settings, string original, params DateTime[] backupTimes)
        {
            var file = _db.InsertFile(original);
            foreach (var bt in backupTimes)
            {
                _db.InsertAttribute(file.Id, bt, bt, bt, bt, 1);
                var backupPath = BackupDb.BackupFileName(settings.DataDir, original, bt);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.WriteAllText(backupPath, "x");
            }
        }

        bool BackupFileExists(Settings settings, string original, DateTime bt)
            => File.Exists(BackupDb.BackupFileName(settings.DataDir, original, bt));

        [TestMethod()]
        public void MaxGenerations_KeepsNewest_DeletesOverflow()
        {
            var settings = MakeSettings(maxGenerations: 2, retentionDays: 0);
            _db = new BackupDb(settings, new LoggerFactoryMock());

            var original = @"C:\Demo\Documents\report.txt";
            var g1 = new DateTime(2026, 1, 1);   // 最古（削除対象）
            var g2 = new DateTime(2026, 2, 1);
            var g3 = new DateTime(2026, 3, 1);   // 最新
            SeedGenerations(settings, original, g1, g2, g3);

            using (var worker = new RetentionWorker(settings, _db, new LoggerFactoryMock()))
            {
                worker.ApplyRetentionPolicy(CancellationToken.None);
            }

            var file = _db.GetFile(original);
            var remaining = _db.GetAttributes(file.Id).Select(a => a.BackupTime).OrderBy(t => t).ToArray();
            CollectionAssert.AreEqual(new[] { g2, g3 }, remaining, "最新2世代のみ残ること");

            Assert.IsFalse(BackupFileExists(settings, original, g1), "溢れた最古のバックアップファイルは削除されること");
            Assert.IsTrue(BackupFileExists(settings, original, g2));
            Assert.IsTrue(BackupFileExists(settings, original, g3));
        }

        [TestMethod()]
        public void RetentionDays_DeletesOld_ButAlwaysKeepsLatest()
        {
            var settings = MakeSettings(maxGenerations: 0, retentionDays: 30);
            _db = new BackupDb(settings, new LoggerFactoryMock());

            var original = @"C:\Demo\Documents\report.txt";
            var old = DateTime.Now.AddDays(-100);   // 保持日数より古い（削除対象）
            var recent = DateTime.Now;              // 最新（常に保持）
            SeedGenerations(settings, original, old, recent);

            using (var worker = new RetentionWorker(settings, _db, new LoggerFactoryMock()))
            {
                worker.ApplyRetentionPolicy(CancellationToken.None);
            }

            var file = _db.GetFile(original);
            var remaining = _db.GetAttributes(file.Id);
            Assert.AreEqual(1, remaining.Count, "古い世代が消え、最新のみ残ること");
            Assert.IsFalse(BackupFileExists(settings, original, old));
            Assert.IsTrue(BackupFileExists(settings, original, recent));
        }

        [TestMethod()]
        public void SingleGeneration_IsNeverDeleted_EvenIfOld()
        {
            // 保持日数より古くても、唯一の世代（＝最新）は削除されない
            var settings = MakeSettings(maxGenerations: 0, retentionDays: 1);
            _db = new BackupDb(settings, new LoggerFactoryMock());

            var original = @"C:\Demo\Documents\report.txt";
            var old = DateTime.Now.AddDays(-100);
            SeedGenerations(settings, original, old);

            using (var worker = new RetentionWorker(settings, _db, new LoggerFactoryMock()))
            {
                worker.ApplyRetentionPolicy(CancellationToken.None);
            }

            var file = _db.GetFile(original);
            Assert.AreEqual(1, _db.GetAttributes(file.Id).Count);
            Assert.IsTrue(BackupFileExists(settings, original, old));
        }
    }
}
