using FileHistory;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileHistory.Tests
{
    [TestClass()]
    public class DirectoryWatcherTest
    {
        IConfiguration? _config;
        string _baseDir = "";
        Settings _settings = new Settings();

        [TestInitialize()]
        public void Initialize()
        {
            // _baseDir :   {TempPath}/{GUID}
            // IncludeDir:  {TempPath}/{GUID}/Watch/Short    0.5
            //                               /Watch/Long     3600
            // DataDir:     {TempPath}/{GUID}/{User}/{Machine}/Data
            _baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_baseDir);
            var watchDir = Path.Combine(_baseDir, "Watch");
            Directory.CreateDirectory(Path.Combine(watchDir, "Long"));
            Directory.CreateDirectory(Path.Combine(watchDir, "Short"));

            _config = new ConfigurationBuilder().AddInMemoryCollection(
                new List<KeyValuePair<string, string?>>
                {
                    new KeyValuePair<string, string?>("Settings:BackupBaseDir", _baseDir),
                    new KeyValuePair<string, string?>("Settings:DefaultBackupInterval", "3600"),
                    new KeyValuePair<string, string?>("Settings:IncludeDirs:0:Dir", watchDir),
                    new KeyValuePair<string, string?>("Settings:IncludeDirs:1:Dir", Path.Combine(watchDir, "Short")),
                    new KeyValuePair<string, string?>("Settings:IncludeDirs:1:BackupInterval", "0.5"),
                    new KeyValuePair<string, string?>("Settings:CrawlingInterval", "0.1"),
                }).Build();
            _settings = new Settings();
            _config.GetSection("Settings").Bind(_settings);
        }

        [TestCleanup()]
        public void Cleanup()
        {
            if(Directory.Exists(_baseDir)) Directory.Delete(_baseDir, true);
        }

        /// <summary>
        /// conditionが満たされるまで最大timeoutMsポーリングする
        /// </summary>
        static bool WaitFor(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Task.Delay(50).Wait();
            }
            return condition();
        }

        [TestMethod()]
        public void CreateFile_Test()
        {
            var loggerBackupDb = new LoggerFactoryMock();
            var backupDb = new BackupDb(_settings, loggerBackupDb);

            var loggerBackupScheduler = new LoggerFactoryMock();
            var backupScheduler = new BackupScheduler(_settings, loggerBackupScheduler, backupDb);

            var loggerDirectoryWatcher = new LoggerFactoryMock();
            var directoryWatcher = new DirectoryWatcher(_settings, loggerDirectoryWatcher, backupScheduler);

            try
            {
                // {_baseDir}/Watch/Short/newfile 作成
                var data1 = new byte[1024];
                new Random().NextBytes(data1);
                var origShortFileName = Path.Combine(_baseDir, "Watch", "Short", "newfile");
                using (var file = File.Create(origShortFileName))
                    file.Write(data1);
                var originalShortCreationTime = File.GetCreationTime(origShortFileName);
                var originalShortLastWriteTime = File.GetLastWriteTime(origShortFileName);
                var originalShortLastAccessTime = File.GetLastAccessTime(origShortFileName);

                // {_baseDir}/Watch/Long/newfile 作成
                var data2 = new byte[1024];
                new Random().NextBytes(data2);
                var origLongFileName = Path.Combine(_baseDir, "Watch", "Long", "newfile");
                using (var file = File.Create(origLongFileName))
                    file.Write(data2);

                // backup先
                var backupBaseDir = Path.Combine(_settings.DataDir, _baseDir.Split(':')[0], _baseDir.Split(':')[1].Substring(1));
                var backupShortDir = Path.Combine(backupBaseDir, "Watch", "Short");
                var backupLongDir = Path.Combine(backupBaseDir, "Watch", "Long");

                // まだバックアップは作成されない (BackupInterval 0.5秒経過前)
                Task.Delay(300).Wait();
                Assert.AreEqual(false, Directory.Exists(backupLongDir));
                Assert.AreEqual(false, Directory.Exists(backupShortDir));

                // Shortのディレクトリだけバックアップ作成 (ファイルコピー後にDB登録されるため両方待つ)
                Assert.IsTrue(WaitFor(() =>
                    Directory.Exists(backupShortDir) &&
                    Directory.EnumerateFiles(backupShortDir, "newfile*").Count() == 1 &&
                    backupDb.GetFile(origShortFileName) != null, 10000),
                    "Short backup was not created in time");
                Assert.AreEqual(false, Directory.Exists(backupLongDir));

                var fileDb = backupDb.GetFile(origShortFileName);
                Assert.IsNotNull(fileDb);
                var attributeDb = backupDb.GetAttributes(fileDb.Id);
                Assert.AreEqual(1, attributeDb.Count);
                var backupShortFileName = BackupDb.BackupFileName(_settings.DataDir, origShortFileName, attributeDb.First().BackupTime);

                // backupのタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, File.GetCreationTime(backupShortFileName));
                Assert.AreEqual(attributeDb.First().LastWriteTime, File.GetLastWriteTime(backupShortFileName));
                Assert.AreEqual(attributeDb.First().LastAccessTime, File.GetLastAccessTime(backupShortFileName));

                // orginalのタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, File.GetCreationTime(origShortFileName));
                Assert.AreEqual(attributeDb.First().LastWriteTime, File.GetLastWriteTime(origShortFileName));

                // backup前のタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, originalShortCreationTime);
                Assert.AreEqual(attributeDb.First().LastWriteTime, originalShortLastWriteTime);
                Assert.AreEqual(attributeDb.First().LastAccessTime, originalShortLastAccessTime);
            }
            finally
            {
                // cleanup (DBを最後に解放しないとCatalog.dbのファイルロックが残る)
                directoryWatcher.Dispose();
                backupScheduler.Dispose();
                backupDb.Dispose();
            }
        }

        [TestMethod()]
        public void Crawler_Test()
        {
            // {_baseDir}/Watch/Short/newfile 作成
            var data1 = new byte[1024];
            new Random().NextBytes(data1);
            var origShortFileName = Path.Combine(_baseDir, "Watch", "Short", "newfile");
            using (var file = File.Create(origShortFileName))
                file.Write(data1);
            var originalShortCreationTime = File.GetCreationTime(origShortFileName);
            var originalShortLastWriteTime = File.GetLastWriteTime(origShortFileName);
            var originalShortLastAccessTime = File.GetLastAccessTime(origShortFileName);

            // {_baseDir}/Watch/Long/newfile 作成
            var data2 = new byte[1024];
            new Random().NextBytes(data2);
            var origLongFileName = Path.Combine(_baseDir, "Watch", "Long", "newfile");
            using (var file = File.Create(origLongFileName))
                file.Write(data2);

            // crawler起動
            var loggerBackupDb = new LoggerFactoryMock();
            var backupDb = new BackupDb(_settings, loggerBackupDb);
            var loggerCrawler = new LoggerFactoryMock();
            var loggerBackupScheduler = new LoggerFactoryMock();
            var backupScheduler = new BackupScheduler(_settings, loggerBackupScheduler, backupDb);
            var crawler = new Crawler(_settings, backupDb, loggerCrawler, backupScheduler);

            try
            {
                // backup先
                var backupBaseDir = Path.Combine(_settings.DataDir, _baseDir.Split(':')[0], _baseDir.Split(':')[1].Substring(1));
                var backupShortDir = Path.Combine(backupBaseDir, "Watch", "Short");
                var backupLongDir = Path.Combine(backupBaseDir, "Watch", "Long");

                // まだバックアップは作成されない (BackupInterval 0.5秒経過前)
                Task.Delay(300).Wait();
                Assert.AreEqual(false, Directory.Exists(backupLongDir));
                Assert.AreEqual(false, Directory.Exists(backupShortDir));

                // Shortのディレクトリだけバックアップ作成 (ファイルコピー後にDB登録されるため両方待つ)
                Assert.IsTrue(WaitFor(() =>
                    Directory.Exists(backupShortDir) &&
                    Directory.EnumerateFiles(backupShortDir, "newfile*").Count() == 1 &&
                    backupDb.GetFile(origShortFileName) != null, 10000),
                    "Short backup was not created in time");
                Assert.AreEqual(false, Directory.Exists(backupLongDir));

                var fileDb = backupDb.GetFile(origShortFileName);
                Assert.IsNotNull(fileDb);
                var attributeDb = backupDb.GetAttributes(fileDb.Id);
                Assert.AreEqual(1, attributeDb.Count);
                var backupShortFileName = BackupDb.BackupFileName(_settings.DataDir, origShortFileName, attributeDb.First().BackupTime);

                // backupのタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, File.GetCreationTime(backupShortFileName));
                Assert.AreEqual(attributeDb.First().LastWriteTime, File.GetLastWriteTime(backupShortFileName));
                Assert.AreEqual(attributeDb.First().LastAccessTime, File.GetLastAccessTime(backupShortFileName));

                // orginalのタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, File.GetCreationTime(origShortFileName));
                Assert.AreEqual(attributeDb.First().LastWriteTime, File.GetLastWriteTime(origShortFileName));

                // backup前のタイムスタンプ = dbのタイムスタンプ
                Assert.AreEqual(attributeDb.First().CreationTime, originalShortCreationTime);
                Assert.AreEqual(attributeDb.First().LastWriteTime, originalShortLastWriteTime);
                Assert.AreEqual(attributeDb.First().LastAccessTime, originalShortLastAccessTime);

                // ２回目以降のクローリングでは新たなバックアップは作成されない
                Task.Delay(1500).Wait();
                Assert.AreEqual(false, Directory.Exists(backupLongDir));
                Assert.AreEqual(1, Directory.EnumerateFiles(backupShortDir, "newfile*").Count());
            }
            finally
            {
                // cleanup (DBを最後に解放しないとCatalog.dbのファイルロックが残る)
                crawler.Dispose();
                backupScheduler.Dispose();
                backupDb.Dispose();
            }
        }
    }
}
