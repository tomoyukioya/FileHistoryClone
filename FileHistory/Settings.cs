using DotNet.Globbing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileHistory
{
    public class Settings
    {
        // appsettings.jsonからバインド
        public string BackupBaseDir { get; set; }
        /// <summary>
        /// 最後にバックアップされてから、指定した秒数間は新たなバックアップを作成しない
        /// </summary>
        public double DefaultBackupInterval { get; set; }
        public List<IncludeDir> IncludeDirs { get; set; }
        public List<string> ExcludeDirs { get; set; }
        /// <summary>
        /// クローリング間隔（秒）
        /// </summary>
        public double CrawlingInterval { get; set; }
        /// <summary>
        /// クローリング開始アイドルタイム（秒）
        /// </summary>
        public double CrawlingIdleTimer { get; set; }
        /// <summary>
        /// UI言語 ("ja", "en" など)。空文字/未指定ならOSの言語に従う
        /// </summary>
        public string Language { get; set; }
        /// <summary>
        /// 1ファイルあたりの最大保持世代数。0以下なら無制限
        /// </summary>
        public int MaxGenerations { get; set; }
        /// <summary>
        /// バックアップ保持日数。0以下なら無制限（最新世代は日数に関わらず常に保持）
        /// </summary>
        public double RetentionDays { get; set; }
        /// <summary>
        /// 保持ポリシー適用スキャンの実行間隔（秒）
        /// </summary>
        public double RetentionScanInterval { get; set; } = 86400;

        /// <summary>
        /// パス中の環境変数（%USERPROFILE% など）を展開する
        /// </summary>
        static string Expand(string path)
            => string.IsNullOrEmpty(path) ? path : Environment.ExpandEnvironmentVariables(path);

        // 基本ディレクトリ要素
        public string DataDir
        {
            get
            {
                return Path.Combine(Expand(BackupBaseDir), Environment.UserName, Environment.MachineName, "Data");
            }
        }

        public string ConfigDir
        {
            get
            {
                return Path.Combine(Expand(BackupBaseDir), Environment.UserName, Environment.MachineName, "Configuration");
            }
        }
        public string BackupDb
        {
            get
            {
                return Path.Combine(ConfigDir, "Catalog.db");
            }
        }

        // クローリングディレクトリ要素
        List<string> _CrawlingBaseDirs = null;
        public List<string> CrawlingBaseDirs
        {
            get
            {
                if (_CrawlingBaseDirs == null)
                {
                    var dirs = new List<string>();
                    foreach (var dir in IncludeDirs
                        .Select(m => Expand(m.Dir).TrimEnd(Path.DirectorySeparatorChar))
                        .OrderBy(m => m.Length))
                    {
                        // ディレクトリ境界を考慮して包含判定 ("C:\Program" が "C:\ProgramData" を包含しないように)
                        if (!dirs.Any(m => dir.Equals(m, StringComparison.OrdinalIgnoreCase) ||
                                           dir.StartsWith(m + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                            dirs.Add(dir);
                    }
                    _CrawlingBaseDirs = dirs;
                }
                return _CrawlingBaseDirs;
            }
        }

        // ExcludeDirsからコンパイルされたマッチャー（遅延初期化）
        // 通常パターンと "!" で始まる再インクルードパターンを分けて保持する
        List<CompiledExcludeMatcher> _excludeMatchers = null;
        List<CompiledExcludeMatcher> _reincludeMatchers = null;
        readonly object _excludeMatchersLock = new object();

        public bool IsExcluded(string path)
        {
            EnsureExcludeMatchers();
            if (_excludeMatchers.Count == 0) return false;
            var normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized)) return false;
            // 再インクルード ("!pattern") が除外より優先される
            if (_reincludeMatchers.Count > 0 && _reincludeMatchers.Any(m => m.IsMatch(normalized))) return false;
            return _excludeMatchers.Any(m => m.IsMatch(normalized));
        }

        void EnsureExcludeMatchers()
        {
            if (_excludeMatchers != null) return;
            lock (_excludeMatchersLock)
            {
                if (_excludeMatchers != null) return;

                var matchers = new List<CompiledExcludeMatcher>();
                var reincludeMatchers = new List<CompiledExcludeMatcher>();
                if (ExcludeDirs != null)
                {
                    var options = new GlobOptions();
                    options.Evaluation.CaseInsensitive = true;
                    foreach (var entry in ExcludeDirs)
                    {
                        if (string.IsNullOrWhiteSpace(entry)) continue;
                        var trimmed = entry.Trim();
                        if (trimmed.StartsWith("#")) continue;

                        // "!" で始まるエントリは再インクルード（除外の例外）
                        var isReinclude = trimmed.StartsWith("!");
                        if (isReinclude) trimmed = trimmed.Substring(1).Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        trimmed = Expand(trimmed);
                        var normPattern = NormalizePath(trimmed);
                        if (string.IsNullOrEmpty(normPattern)) continue;

                        var isAbsolute = IsAbsolutePathPattern(trimmed);
                        var matcher = new CompiledExcludeMatcher(normPattern, isAbsolute, options);
                        if (isReinclude) reincludeMatchers.Add(matcher);
                        else matchers.Add(matcher);
                    }
                }
                _reincludeMatchers = reincludeMatchers;
                _excludeMatchers = matchers;
            }
        }

        // パスを '/' 区切りに正規化し、末尾の区切り文字を削除
        static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var s = path.Replace('\\', '/');
            if (s.Length > 1)
                s = s.TrimEnd('/');
            return s;
        }

        // パターンが絶対パス（Windowsドライブレター始まり）か判定
        static bool IsAbsolutePathPattern(string pattern)
        {
            return pattern.Length >= 2 && char.IsLetter(pattern[0]) && pattern[1] == ':';
        }

        // 1エントリ分のコンパイル済みマッチャー
        class CompiledExcludeMatcher
        {
            readonly Glob _selfGlob;
            readonly Glob _descendantsGlob;

            public CompiledExcludeMatcher(string normPattern, bool isAbsolute, GlobOptions options)
            {
                if (isAbsolute)
                {
                    _selfGlob = Glob.Parse(normPattern, options);
                    _descendantsGlob = Glob.Parse(normPattern + "/**", options);
                }
                else
                {
                    _selfGlob = Glob.Parse("**/" + normPattern, options);
                    _descendantsGlob = Glob.Parse("**/" + normPattern + "/**", options);
                }
            }

            public bool IsMatch(string normalizedPath)
            {
                return _selfGlob.IsMatch(normalizedPath) || _descendantsGlob.IsMatch(normalizedPath);
            }
        }

        // BackupInterval判定用の事前計算済みルール（最長プレフィックス優先、遅延初期化）
        List<KeyValuePair<string, double>> _intervalRules = null;
        readonly object _intervalRulesLock = new object();

        List<KeyValuePair<string, double>> EnsureIntervalRules()
        {
            if (_intervalRules != null) return _intervalRules;
            lock (_intervalRulesLock)
            {
                if (_intervalRules != null) return _intervalRules;
                _intervalRules = (IncludeDirs ?? new List<IncludeDir>())
                    .Where(m => m.BackupInterval != null && !string.IsNullOrEmpty(m.Dir))
                    .Select(m => new KeyValuePair<string, double>(
                        Expand(m.Dir).ToLowerInvariant().TrimEnd(Path.DirectorySeparatorChar),
                        m.BackupInterval.Value))
                    .OrderByDescending(m => m.Key.Length)
                    .ToList();
                return _intervalRules;
            }
        }

        public double BackupInterval(string path)
        {
            var trimPath = path.ToLowerInvariant().TrimEnd(Path.DirectorySeparatorChar);
            foreach (var rule in EnsureIntervalRules())
            {
                // ディレクトリ境界を考慮したプレフィックス一致
                if (trimPath.Length == rule.Key.Length)
                {
                    if (trimPath == rule.Key) return rule.Value;
                }
                else if (trimPath.Length > rule.Key.Length &&
                         trimPath[rule.Key.Length] == Path.DirectorySeparatorChar &&
                         trimPath.StartsWith(rule.Key))
                    return rule.Value;
            }
            return DefaultBackupInterval;
        }
    }

    public class IncludeDir
    {
        public string Dir { get; set; }
        public double? BackupInterval { get; set; }
    }
}
