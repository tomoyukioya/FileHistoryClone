using FileHistory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace FileHistory.Tests
{
    [TestClass()]
    public class BackupDbTests
    {
        string _baseDir = "";
        Settings _settings = new Settings();
        BackupDb _db = null!;

        [TestInitialize()]
        public void Initialize()
        {
            _baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_baseDir);
            _settings = new Settings { BackupBaseDir = _baseDir };
            _db = new BackupDb(_settings, new LoggerFactoryMock());
        }

        [TestCleanup()]
        public void Cleanup()
        {
            _db?.Dispose();
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, true);
        }

        [TestMethod()]
        public void BackupFileName_ComposesExpectedPath()
        {
            var dt = new DateTime(2026, 7, 8, 12, 34, 56);
            var result = BackupDb.BackupFileName(@"C:\Data", @"C:\Demo\Documents\report.txt", dt);
            Assert.AreEqual(@"C:\Data\C\Demo\Documents\report(2026_07_08 12_34_56).txt", result);
        }

        [TestMethod()]
        public void InsertFile_CreatesDirectoryTree_AndDeduplicates()
        {
            var path = @"C:\Demo\Documents\report.txt";
            var f1 = _db.InsertFile(path);
            var f2 = _db.InsertFile(path);   // 2回目は同じエントリを返すこと（重複を作らない）
            Assert.AreEqual(f1.Id, f2.Id);
            Assert.AreEqual("report.txt", f1.Name);

            var found = _db.GetFile(path);
            Assert.IsNotNull(found);
            Assert.AreEqual(f1.Id, found.Id);

            // ディレクトリ解決
            var dir = _db.GetDirectryFromFilePath(path);
            Assert.IsNotNull(dir);
            Assert.AreEqual("Documents", dir.Name);
            var dir2 = _db.GetDirectryFromDirPath(@"C:\Demo\Documents");
            Assert.AreEqual(dir.Id, dir2.Id);

            // GetFileDir はツリーからパスを再構築する
            var fileDir = _db.GetFileDir(f1.Id).TrimEnd(Path.DirectorySeparatorChar);
            Assert.AreEqual(@"C:\Demo\Documents", fileDir);
        }

        [TestMethod()]
        public void GetFile_ReturnsNull_ForUnknownPath()
        {
            Assert.IsNull(_db.GetFile(@"C:\Nope\missing.txt"));
        }

        [TestMethod()]
        public void Attributes_Insert_GetLatest_GetExact_Delete()
        {
            var f = _db.InsertFile(@"C:\Demo\a.txt");
            var t1 = new DateTime(2026, 1, 1, 0, 0, 0);
            var t2 = new DateTime(2026, 2, 1, 0, 0, 0);
            _db.InsertAttribute(f.Id, DateTime.Now, t1, t1, t1, 10);  // 古い世代
            _db.InsertAttribute(f.Id, DateTime.Now, t2, t2, t2, 20);  // 新しい世代

            Assert.AreEqual(2, _db.GetAttributes(f.Id).Count);

            // LastUpdate = max(CreationTime, LastWriteTime) の降順で最新を返す
            var latest = _db.GetLatestAttribute(f.Id);
            Assert.AreEqual(t2, latest.LastWriteTime);

            var exact = _db.GetAttribute(f.Id, t1, t1, 10);
            Assert.IsNotNull(exact);
            Assert.AreEqual(10, exact.Size);
            Assert.IsNull(_db.GetAttribute(f.Id, t1, t1, 999), "サイズ不一致は見つからないこと");

            Assert.IsTrue(_db.DeleteAttribute(exact.Id));
            Assert.AreEqual(1, _db.GetAttributes(f.Id).Count);
        }

        [TestMethod()]
        public void DeleteFile_OnlyWhenNoAttributesRemain()
        {
            var f = _db.InsertFile(@"C:\Demo\b.txt");
            var t = new DateTime(2026, 1, 1);
            _db.InsertAttribute(f.Id, DateTime.Now, t, t, t, 5);

            Assert.IsFalse(_db.DeleteFile(f.Id), "属性が残っているファイルは削除しないこと");

            var attr = _db.GetAttributes(f.Id).Single();
            _db.DeleteAttribute(attr.Id);
            Assert.IsTrue(_db.DeleteFile(f.Id), "属性が無ければ削除できること");
            Assert.IsNull(_db.GetFile(@"C:\Demo\b.txt"));
        }

        [TestMethod()]
        public void GetChildren_ReturnsDirsAndFiles()
        {
            _db.InsertFile(@"C:\Demo\Documents\a.txt");
            _db.InsertFile(@"C:\Demo\Documents\b.txt");
            _db.InsertFile(@"C:\Demo\Pictures\c.txt");

            var demo = _db.GetDirectryFromDirPath(@"C:\Demo");
            var childDirs = _db.GetChildDirectories(demo.Id).Select(d => d.Name).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "Documents", "Pictures" }, childDirs);

            var docs = _db.GetDirectryFromDirPath(@"C:\Demo\Documents");
            var childFiles = _db.GetChildFiles(docs.Id).Select(f => f.Name).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "a.txt", "b.txt" }, childFiles);
        }

        [TestMethod()]
        public void DeleteDirectoryIfEmpty_RemovesEmptyChain_ButKeepsNonEmpty()
        {
            var f = _db.InsertFile(@"C:\Demo\Documents\only.txt");
            var docs = _db.GetDirectryFromDirPath(@"C:\Demo\Documents");

            // ファイルが存在する間はディレクトリを削除しない
            _db.DeleteDirectoryIfEmpty(docs.Id);
            Assert.IsNotNull(_db.GetDirectryFromDirPath(@"C:\Demo\Documents"));

            // ファイルを消すと Documents -> Demo -> C: の空チェーンがまとめて消える
            _db.DeleteFile(f.Id);
            _db.DeleteDirectoryIfEmpty(docs.Id);
            Assert.IsNull(_db.GetDirectryFromDirPath(@"C:\Demo\Documents"));
            Assert.IsNull(_db.GetDirectryFromDirPath(@"C:\Demo"));
        }

        [TestMethod()]
        public void FindAllFiles_ReturnsEveryInsertedFile()
        {
            _db.InsertFile(@"C:\X\1.txt");
            _db.InsertFile(@"D:\Y\2.txt");
            Assert.AreEqual(2, _db.FindAllFiles().Count());
        }
    }
}
