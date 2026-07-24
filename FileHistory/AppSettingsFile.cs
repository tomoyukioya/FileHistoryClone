using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FileHistory
{
    /// <summary>
    /// 設定 GUI が編集する項目のデータモデル。
    /// </summary>
    public class AppSettingsData
    {
        public string BackupBaseDir { get; set; } = "%USERPROFILE%\\FileHistoryCloneBackup";
        public double DefaultBackupInterval { get; set; } = 3600;
        public double CrawlingIdleTimer { get; set; } = 60;
        public double CrawlingInterval { get; set; } = 86400;
        public int MaxGenerations { get; set; } = 0;
        public double RetentionDays { get; set; } = 0;
        public double RetentionScanInterval { get; set; } = 86400;
        public string Language { get; set; } = "";
        public List<IncludeDir> IncludeDirs { get; set; } = new List<IncludeDir>();
        public List<string> ExcludeDirs { get; set; } = new List<string>();
    }

    /// <summary>
    /// appsettings.json の "Settings" セクションを読み書きする(Logging 等は温存)。
    /// コメント付き JSON も読み込める。UI から分離してテスト可能にしている。
    /// </summary>
    public static class AppSettingsFile
    {
        static readonly JsonDocumentOptions DocOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        // TypeInfoResolver を明示しないと単一ファイル発行などの構成で
        // JsonNode.ToJsonString がリフレクション不足で失敗するため指定する。
        static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        public static AppSettingsData Load(string path)
        {
            var data = new AppSettingsData();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return data;

            JsonObject settings;
            try { settings = (JsonNode.Parse(File.ReadAllText(path), null, DocOptions) as JsonObject)?["Settings"] as JsonObject; }
            catch { return data; }   // 壊れた設定ファイルなら既定値を返す
            if (settings == null) return data;

            data.BackupBaseDir = GetString(settings, "BackupBaseDir", data.BackupBaseDir);
            data.DefaultBackupInterval = GetDouble(settings, "DefaultBackupInterval", data.DefaultBackupInterval);
            data.CrawlingIdleTimer = GetDouble(settings, "CrawlingIdleTimer", data.CrawlingIdleTimer);
            data.CrawlingInterval = GetDouble(settings, "CrawlingInterval", data.CrawlingInterval);
            data.MaxGenerations = (int)GetDouble(settings, "MaxGenerations", data.MaxGenerations);
            data.RetentionDays = GetDouble(settings, "RetentionDays", data.RetentionDays);
            data.RetentionScanInterval = GetDouble(settings, "RetentionScanInterval", data.RetentionScanInterval);
            data.Language = GetString(settings, "Language", data.Language);

            if (settings["IncludeDirs"] is JsonArray inc)
            {
                foreach (var item in inc)
                {
                    var dir = TryGetString(item?["Dir"]);
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    double? interval = null;
                    if (item["BackupInterval"] is JsonValue bv && bv.TryGetValue<double>(out var d)) interval = d;
                    data.IncludeDirs.Add(new IncludeDir { Dir = dir, BackupInterval = interval });
                }
            }
            if (settings["ExcludeDirs"] is JsonArray exc)
                foreach (var item in exc) { var s = TryGetString(item); if (s != null) data.ExcludeDirs.Add(s); }

            return data;
        }

        public static void Save(string path, AppSettingsData data)
        {
            JsonObject root = null;
            if (File.Exists(path))
            {
                // 既存ファイルの Logging 等を温存するために読み込むが、壊れていても
                // 新規から書き直す(GUI が Settings を上書きするため実害は最小)。
                try { root = JsonNode.Parse(File.ReadAllText(path), null, DocOptions) as JsonObject; }
                catch { root = null; }
            }
            root ??= new JsonObject();

            var settings = root["Settings"] as JsonObject ?? new JsonObject();

            settings["BackupBaseDir"] = data.BackupBaseDir ?? "";
            settings["DefaultBackupInterval"] = data.DefaultBackupInterval;
            settings["CrawlingIdleTimer"] = data.CrawlingIdleTimer;
            settings["CrawlingInterval"] = data.CrawlingInterval;
            settings["MaxGenerations"] = data.MaxGenerations;
            settings["RetentionDays"] = data.RetentionDays;
            settings["RetentionScanInterval"] = data.RetentionScanInterval;
            settings["Language"] = data.Language ?? "";

            var incArr = new JsonArray();
            foreach (var d in data.IncludeDirs)
            {
                if (string.IsNullOrWhiteSpace(d.Dir)) continue;
                var o = new JsonObject { ["Dir"] = d.Dir };
                if (d.BackupInterval != null) o["BackupInterval"] = d.BackupInterval.Value;
                incArr.Add(o);
            }
            settings["IncludeDirs"] = incArr;

            var excArr = new JsonArray();
            foreach (var e in data.ExcludeDirs.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                excArr.Add(e);
            settings["ExcludeDirs"] = excArr;

            root["Settings"] = settings;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, root.ToJsonString(WriteOptions));
        }

        static string GetString(JsonObject o, string key, string def)
        { var s = TryGetString(o?[key]); return s ?? def; }
        static string TryGetString(JsonNode n)
        { try { return n?.GetValue<string>(); } catch { return null; } }
        static double GetDouble(JsonObject o, string key, double def)
        { try { if (o?[key] is JsonValue v && v.TryGetValue<double>(out var d)) return d; } catch { } return def; }
    }
}
