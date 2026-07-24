using Microsoft.VisualStudio.TestTools.UnitTesting;
using FileHistory;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileHistory.Tests
{
    [TestClass()]
    public class SettingsTests
    {
        [TestMethod()]
        public void DataDir_Test()
        {
            var settings = new Settings
            {
                BackupBaseDir = "C:\\dir1",
            };
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\BackupFiles", settings.DataDir);

            settings.BackupBaseDir = "C:\\dir1\\";
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\BackupFiles", settings.DataDir);
        }

        [TestMethod()]
        public void ConfigDir_Test()
        {
            var settings = new Settings
            {
                BackupBaseDir = "C:\\dir1",
            };
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\Database", settings.ConfigDir);

            settings.BackupBaseDir = "C:\\dir1\\";
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\Database", settings.ConfigDir);
        }

        [TestMethod()]
        public void BackupDb_Test()
        {
            var settings = new Settings
            {
                BackupBaseDir = "C:\\dir1",
            };
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\Database\\Catalog.db", settings.BackupDb);

            settings.BackupBaseDir = "C:\\dir1\\";
            Assert.AreEqual($"C:\\dir1\\{Environment.UserName}\\{Environment.MachineName}\\Database\\Catalog.db", settings.BackupDb);
        }

        [TestMethod()]
        public void CrawlingBaseDirs_BackupInterval_Test()
        {
            var settings = new Settings
            {
                DefaultBackupInterval = 10,
                IncludeDirs = new List<IncludeDir>
                {
                    new IncludeDir
                    {
                        Dir = "C:\\dirdir3\\dir31\\",
                        BackupInterval = 60,
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dir1",
                        BackupInterval = 20,
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dir1\\dir11",
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dir1\\dir11\\dir111\\",
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dir1\\dir12\\",
                        BackupInterval= 30,
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dir1\\dir12\\dir121",
                        BackupInterval = 70,
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\d2\\",
                        BackupInterval = 40,
                    },
                    new IncludeDir
                    {
                        Dir = "C:\\dirdir3",
                        BackupInterval = 50,
                    },
                },
            };

            // cacheなし
            var crawlingBaseDirs = settings.CrawlingBaseDirs;
            Assert.AreEqual(3, crawlingBaseDirs.Count);
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\dir1"));
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\d2"));
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\dirdir3"));

            // cacheあり
            crawlingBaseDirs = settings.CrawlingBaseDirs;
            Assert.AreEqual(3, crawlingBaseDirs.Count);
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\dir1"));
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\d2"));
            Assert.AreEqual(true, crawlingBaseDirs.Contains(@"C:\dirdir3"));

            // BackupInterval
            Assert.AreEqual(10, settings.BackupInterval(@"D:\"));
            Assert.AreEqual(20, settings.BackupInterval(@"C:\DIR1"));
            Assert.AreEqual(20, settings.BackupInterval(@"C:\DIR1\"));
            Assert.AreEqual(20, settings.BackupInterval(@"C:\DIR1\dir2"));
            Assert.AreEqual(20, settings.BackupInterval(@"C:\dir1\dir11"));
            Assert.AreEqual(20, settings.BackupInterval(@"C:\DIR1\dir11\dir111"));
            Assert.AreEqual(30, settings.BackupInterval(@"C:\DIR1\dir12"));
            Assert.AreEqual(30, settings.BackupInterval(@"C:\DIR1\dir12\"));
            Assert.AreEqual(70, settings.BackupInterval(@"C:\DIR1\dir12\dir121"));
            Assert.AreEqual(30, settings.BackupInterval(@"C:\DIR1\dir12\dir122"));
            Assert.AreEqual(30, settings.BackupInterval(@"C:\DIR1\dir12\dir122\dir1221"));
            Assert.AreEqual(40, settings.BackupInterval(@"C:\D2"));
            Assert.AreEqual(40, settings.BackupInterval(@"C:\D2\dir1"));
            Assert.AreEqual(40, settings.BackupInterval(@"C:\D2\dir1\"));
            Assert.AreEqual(50, settings.BackupInterval(@"C:\dirdir3"));
            Assert.AreEqual(50, settings.BackupInterval(@"C:\dirdir3\"));
            Assert.AreEqual(50, settings.BackupInterval(@"C:\dIRdir3\DIR"));
        }

        // ---- IsExcluded / ExcludeDirs パターンマッチ ----

        static Settings MakeSettings(params string[] excludeDirs)
        {
            return new Settings { ExcludeDirs = new List<string>(excludeDirs) };
        }

        [TestMethod()]
        public void IsExcluded_NullOrEmpty_ReturnsFalse()
        {
            var s1 = new Settings { ExcludeDirs = null };
            Assert.IsFalse(s1.IsExcluded(@"C:\anywhere"));

            var s2 = MakeSettings();
            Assert.IsFalse(s2.IsExcluded(@"C:\anywhere"));
        }

        [TestMethod()]
        public void IsExcluded_AbsolutePath_ExactAndDescendants()
        {
            var s = MakeSettings(@"C:\Users\tomoy\.android");
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\.android"));
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\.android\"));
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\.android\sub\file.txt"));
        }

        [TestMethod()]
        public void IsExcluded_AbsolutePath_CaseInsensitive()
        {
            var s = MakeSettings(@"C:\Users\tomoy\.android");
            Assert.IsTrue(s.IsExcluded(@"c:\users\TOMOY\.Android"));
            Assert.IsTrue(s.IsExcluded(@"C:\USERS\tomoy\.ANDROID\foo"));
        }

        [TestMethod()]
        public void IsExcluded_AbsolutePath_RespectsDirectoryBoundary()
        {
            // 現行の StartsWith バグ解消: .android と .androidXYZ は別物
            var s = MakeSettings(@"C:\Users\tomoy\.android");
            Assert.IsFalse(s.IsExcluded(@"C:\Users\tomoy\.androidXYZ"));
            Assert.IsFalse(s.IsExcluded(@"C:\Users\tomoy\.androidXYZ\sub"));
        }

        [TestMethod()]
        public void IsExcluded_RelativeName_MatchesAnyDepth()
        {
            var s = MakeSettings(".git");
            Assert.IsTrue(s.IsExcluded(@"C:\a\b\.git"));
            Assert.IsTrue(s.IsExcluded(@"C:\a\b\.git\HEAD"));
            Assert.IsTrue(s.IsExcluded(@"D:\.git"));
            Assert.IsTrue(s.IsExcluded(@"D:\.git\refs\heads\main"));
        }

        [TestMethod()]
        public void IsExcluded_RelativeName_DoesNotMatchPartialComponent()
        {
            var s = MakeSettings(".git");
            Assert.IsFalse(s.IsExcluded(@"C:\a\b\mygit"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\b\.gitignore"));
        }

        [TestMethod()]
        public void IsExcluded_Wildcard_StarMatchesFileName()
        {
            var s = MakeSettings("*.tmp");
            Assert.IsTrue(s.IsExcluded(@"C:\a\foo.tmp"));
            Assert.IsTrue(s.IsExcluded(@"D:\x\y\bar.tmp"));
            Assert.IsTrue(s.IsExcluded(@"C:\a\foo.tmp\descendant"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\foo.txt"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\tmp"));
        }

        [TestMethod()]
        public void IsExcluded_MultiComponentRelative()
        {
            var s = MakeSettings("bin/Debug");
            Assert.IsTrue(s.IsExcluded(@"C:\proj\bin\Debug"));
            Assert.IsTrue(s.IsExcluded(@"C:\proj\bin\Debug\app.exe"));
            Assert.IsFalse(s.IsExcluded(@"C:\proj\Debug"));
            Assert.IsFalse(s.IsExcluded(@"C:\proj\bin\Release"));
        }

        [TestMethod()]
        public void IsExcluded_IgnoresEmptyAndCommentEntries()
        {
            var s = MakeSettings("", "   ", "# this is a comment", "  # padded comment", ".git");
            Assert.IsTrue(s.IsExcluded(@"C:\a\.git"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\normal"));
        }

        [TestMethod()]
        public void IsExcluded_ForwardSlashPatternWorks()
        {
            // パターン内の区切り文字は / でも \ でも動くこと
            var s = MakeSettings("C:/Users/tomoy/AppData");
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\AppData"));
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\AppData\Local\foo"));
        }

        [TestMethod()]
        public void IsExcluded_ReincludePattern_OverridesExclude()
        {
            // "!" で始まるパターンは除外の例外（再インクルード）
            var s = MakeSettings("*.log", "!important.log");
            Assert.IsTrue(s.IsExcluded(@"C:\a\app.log"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\important.log"));
            Assert.IsFalse(s.IsExcluded(@"C:\a\b\important.log"));
        }

        [TestMethod()]
        public void IsExcluded_EnvironmentVariable_Expanded()
        {
            Environment.SetEnvironmentVariable("FHC_TEST_DIR", @"C:\Users\tomoy");
            try
            {
                var s = MakeSettings(@"%FHC_TEST_DIR%\AppData");
                Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\AppData"));
                Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\AppData\Local\foo"));
                Assert.IsFalse(s.IsExcluded(@"C:\Users\tomoy\Documents"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("FHC_TEST_DIR", null);
            }
        }

        [TestMethod()]
        public void CrawlingBaseDirs_EnvironmentVariable_Expanded()
        {
            Environment.SetEnvironmentVariable("FHC_TEST_DIR2", @"C:\basedir");
            try
            {
                var settings = new Settings
                {
                    IncludeDirs = new List<IncludeDir>
                    {
                        new IncludeDir { Dir = @"%FHC_TEST_DIR2%\dir1" },
                        new IncludeDir { Dir = @"%FHC_TEST_DIR2%\dir1\sub" },
                    },
                };
                Assert.AreEqual(1, settings.CrawlingBaseDirs.Count);
                Assert.AreEqual(@"C:\basedir\dir1", settings.CrawlingBaseDirs[0]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("FHC_TEST_DIR2", null);
            }
        }

        [TestMethod()]
        public void CrawlingBaseDirs_RespectsDirectoryBoundary()
        {
            // "C:\Program" が "C:\ProgramData" を包含しないこと
            var settings = new Settings
            {
                IncludeDirs = new List<IncludeDir>
                {
                    new IncludeDir { Dir = @"C:\Program" },
                    new IncludeDir { Dir = @"C:\ProgramData" },
                },
            };
            Assert.AreEqual(2, settings.CrawlingBaseDirs.Count);
        }

        [TestMethod()]
        public void IsExcluded_MultiplePatterns_AnyMatches()
        {
            var s = MakeSettings(
                @"C:\Users\tomoy\AppData",
                ".git",
                "*.tmp",
                "node_modules");
            Assert.IsTrue(s.IsExcluded(@"C:\Users\tomoy\AppData\Local"));
            Assert.IsTrue(s.IsExcluded(@"D:\code\.git\HEAD"));
            Assert.IsTrue(s.IsExcluded(@"E:\work\draft.tmp"));
            Assert.IsTrue(s.IsExcluded(@"C:\proj\src\node_modules\x\index.js"));
            Assert.IsFalse(s.IsExcluded(@"C:\Users\tomoy\Documents\memo.txt"));
        }
    }
}