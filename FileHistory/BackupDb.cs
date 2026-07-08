using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHistory
{
    public interface IBackupDb
    {
        AttributeDbEntry GetLatestAttribute(int fileId);
        AttributeDbEntry GetAttribute(int fileId, DateTime creationTime, DateTime lastWriteTime, long size);
        FileDbEntry GetFile(string fullPath, DirectoryDbEntry entry = null);
        void InsertAttribute(int fileId, DateTime backupTime, DateTime creationTime, DateTime lastWriteTime, DateTime lastAccessTime, long size);
        bool DeleteAttribute(int attributeId);
        FileDbEntry InsertFile(string fullPath);
        Task<int> FileCount(CancellationToken token);
        Task<int> AttributeCount(CancellationToken token);
        List<DirectoryDbEntry> GetChildDirectories(int parentId);
        List<FileDbEntry> GetChildFiles(int directoryId);
        List<AttributeDbEntry> GetAttributes(int fileId);
        DirectoryDbEntry GetDirectryFromFilePath(string fullPath);
        DirectoryDbEntry GetDirectryFromDirPath(string dirPath);
        string GetFileDir(int fileId);
        IEnumerable<FileDbEntry> FindAllFiles();
        void Dispose();
        bool DeleteFile(int fileId);
        void DeleteDirectoryIfEmpty(int directoryId);
    }

    /// <summary>
    /// バックアップファイルを記録するDB
    /// </summary>
    public class BackupDb : IBackupDb, IDisposable
    {
        // DI
        readonly ILogger _logger;
        readonly Settings _settings;

        // LiteDB
        readonly LiteDatabase _db;
        readonly ILiteCollection<FileDbEntry> _fileDbEntries;
        readonly ILiteCollection<AttributeDbEntry> _fileAttributeDbEntries;
        readonly ILiteCollection<DirectoryDbEntry> _directoryDbEntries;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BackupDb(Settings settings, ILoggerFactory loggerFactory)
        {
            // DI
            _logger = loggerFactory.CreateLogger<BackupDb>();
            _logger?.LogTrace("Enter: BackupDb()");
            _settings = settings;

            // mapper
            // https://github.com/mbdavid/LiteDB/issues/1765
            var mapper = new BsonMapper();
            mapper.RegisterType<DateTime>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            mapper.RegisterType<DateTimeOffset>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            Directory.CreateDirectory(_settings.ConfigDir);
            _db = new LiteDatabase(_settings.BackupDb, mapper);
            _fileAttributeDbEntries = _db.GetCollection<AttributeDbEntry>("AttributeDbEntries");
            _fileAttributeDbEntries.EnsureIndex(m => m.FileId);
            _fileDbEntries = _db.GetCollection<FileDbEntry>("FileDbEntries");
            _fileDbEntries.EnsureIndex(m => m.DirectoryId);
            _directoryDbEntries = _db.GetCollection<DirectoryDbEntry>("DirectoryDbEntries");
            _directoryDbEntries.EnsureIndex(m => m.ParentId);

            _logger?.LogTrace("Leave: BackupDb()");
        }

        readonly object InsertFileLock = new();
        /// <summary>
        /// Fileエントリ情報の作成
        /// </summary>
        public FileDbEntry InsertFile(string fullPath)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}, {fullpath}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "", fullPath);
                var directory = CreateDirectryIfNotExists(fullPath);
                lock (InsertFileLock)
                {
                    var existingFile = GetFile(fullPath);
                    if (existingFile == null)
                    {
                        var file = new FileDbEntry
                        {
                            DirectoryId = directory.Id,
                            Name = Path.GetFileName(fullPath),
                        };
                        _fileDbEntries.Insert(file);
                        return file;
                    }
                    else
                        return existingFile;
                }
            }
            catch (Exception ex) { 
                _logger?.LogError("Exception caught: {ex}", ex.ToString());
                throw;
            }
            finally { 
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// Fileエントリ数の取得
        /// </summary>
        public async Task<int> FileCount(CancellationToken token)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                return await Task.Factory.StartNew(() => _fileDbEntries.Count(), token).ConfigureAwait(false);
            }
            catch (Exception ex) 
            {
                _logger?.LogError("Exception caught: {ex}", ex.ToString());
                return 0;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// Attributeエントリ数の取得
        /// </summary>
        public async Task<int> AttributeCount(CancellationToken token)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                return await Task.Factory.StartNew(() => _fileAttributeDbEntries.Count(), token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Exception caught: {ex}", ex.ToString());
                return 0;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// バックアップファイル属性の追加
        /// </summary>
        public void InsertAttribute(int fileId, DateTime backupTime, DateTime creationTime, DateTime lastWriteTime, DateTime lastAccessTime, long size)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}, fileId = {fileId}, backupTime = {backupTime}, creationTime = {creationTime}, " +
                    "lastWriteTime = {lastWriteTime}, lastAccessTime = {lastAccessTime}, lsize = {size}", 
                    System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "",
                    fileId, backupTime, creationTime, lastWriteTime, lastAccessTime, size);
                _fileAttributeDbEntries.Insert(new AttributeDbEntry
                {
                    FileId = fileId,
                    BackupTime = backupTime,
                    CreationTime = creationTime,
                    LastWriteTime = lastWriteTime,
                    LastAccessTime = lastAccessTime,
                    Size = size,
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError("Exception caught: {ex}", ex.ToString());
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// バックアップファイル属性の取得
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns></returns>
        public AttributeDbEntry GetAttribute(int fileId, DateTime creationTime, DateTime lastWriteTime, long size)
        {
            var attribute = _fileAttributeDbEntries.Find(m => m.FileId == fileId)
                .Where(m => m.CreationTime == creationTime && m.LastWriteTime == lastWriteTime && m.Size == size)
                .FirstOrDefault();

            //var attribute = _fileAttributeDbEntries.FindOne(
            //    Query.And(
            //        Query.EQ("FileId", fileId),
            //        Query.EQ("CreationTime", creationTime.ToString("o", CultureInfo.InvariantCulture)),
            //        Query.EQ("LastWriteTime", lastWriteTime.ToString("o", CultureInfo.InvariantCulture)),
            //        Query.EQ("Size", size)
            //        ));

            return attribute;
        }
        /// <summary>
        /// バックアップファイル属性の取得
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns></returns>
        public AttributeDbEntry GetLatestAttribute(int fileId)
        {
            return GetAttributes(fileId).OrderByDescending(m => m.LastUpdate).FirstOrDefault();
        }

        public List<AttributeDbEntry> GetAttributes(int fileId)
        {
            return _fileAttributeDbEntries.Find(m => m.FileId == fileId).ToList();
        }

        /// <summary>
        /// バックアップファイル属性の削除
        /// </summary>
        /// <param name="attributeId"></param>
        /// <returns></returns>
        public bool DeleteAttribute(int attributeId)
        {
            return _fileAttributeDbEntries.Delete(attributeId);
        }

        public bool DeleteFile(int fileId)
        {
            var attr = _fileAttributeDbEntries.FindOne(m => m.FileId == fileId);
            if (attr == null)
            {
                return _fileDbEntries.Delete(fileId);
            }
            else
                return false;
        }

        /// <summary>
        /// 空ディレクトリを削除
        /// </summary>
        public void DeleteDirectoryIfEmpty(int directoryId)
        {
            try
            {
                _logger.LogTrace($"Enter: DeleteDirectoryIfEmpty({directoryId})");
                DirectoryDbEntry current = null;

                lock (_deleteCreateDirectryLock)
                {
                    // ルートだと終了
                    if (directoryId == -1) return;

                    // ファイルがある場合は削除しない
                    var file = _fileDbEntries.FindOne(m => m.DirectoryId == directoryId);
                    if (file != null) return;

                    // ディレクトリエントリー取得
                    current = _directoryDbEntries.FindOne(m => m.Id == directoryId);
                    if (current == null) return;

                    // ディレクトリがある場合は削除しない
                    var childDir = _directoryDbEntries.FindOne(m => m.ParentId == current.Id);
                    if (childDir != null) return;

                    // ディレクトリ削除
                    var backupDirPath = "";
                    var ptr = current;
                    while (ptr.ParentId != -1)
                    {
                        backupDirPath = Path.Combine(ptr.Name, backupDirPath);
                        var parentId = ptr.ParentId;
                        ptr = _directoryDbEntries.FindOne(m => m.Id == parentId);
                        if (ptr == null)
                        {
                            _logger.LogError($"DirectoryDbEntry({parentId}) not found.");
                            return;
                        }
                    }
                    backupDirPath = Path.Combine(_settings.DataDir, ptr.Name.TrimEnd(':'), backupDirPath);

                    _logger.LogTrace($"Delete Directory ({directoryId}), {backupDirPath}");
                    try
                    {
                        if (Directory.Exists(backupDirPath))
                            Directory.Delete(backupDirPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Exception caught in deleting {backupDirPath}: {ex}");
                    }
                    _directoryDbEntries.Delete(directoryId);
                }

                // 親ディレクトリを再帰的に削除
                DeleteDirectoryIfEmpty(current.ParentId);
            }
            catch(Exception ex)
            {
                _logger.LogError($"Exception caught in DeleteDirectoryIfEmpty(): {ex}");
            }
            finally
            {
                _logger.LogTrace($"Leave: DeleteDirectoryIfEmpty({directoryId})");
            }
        }

        /// <summary>
        /// fullPathからFileDbEntryを検索
        /// 見つからなければnull
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public FileDbEntry GetFile(string fullPath, DirectoryDbEntry entry = null)
        {
            if (entry == null) entry = GetDirectryFromFilePath(fullPath);
            if (entry == null) return null;

            var result = _fileDbEntries.FindOne(m => m.DirectoryId == entry.Id && m.Name == Path.GetFileName(fullPath));

            //var result = _fileDbEntries.FindOne(
            //    Query.And(
            //        Query.EQ("DirectoryId", entry.Id),
            //        Query.EQ("Name", Path.GetFileName(fullPath))
            //        ));

            return result;
        }

        public List<DirectoryDbEntry> GetChildDirectories(int parentId)
        {
            return _directoryDbEntries.Find(m => m.ParentId == parentId).ToList();
        }

        public List<FileDbEntry> GetChildFiles(int directoryId)
        {
            return _fileDbEntries.Find(m => m.DirectoryId == directoryId).ToList();
        }

        /// <summary>
        /// ファイル名を含むフルパスから、対応するDirectoryDbEntryを見つける
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public DirectoryDbEntry GetDirectryFromFilePath(string filePath)
        {
            var paths = filePath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            var qname = paths[0];
            var current = _directoryDbEntries.FindOne(m => m.ParentId == -1 && m.Name == qname);
            //var current = _directoryDbEntries.Find(Query.And(Query.EQ("ParentId", -1), Query.EQ("Name", paths[0]))).FirstOrDefault();
            if (current == null) return null;

            // "C:\file.ext"
            if (paths.Length == 2) return current;

            foreach (var item in paths.Skip(1).Take(paths.Length - 2))
            {
                current = _directoryDbEntries.FindOne(m => m.ParentId == current.Id && m.Name == item);
                //current = _directoryDbEntries.FindOne(Query.And(Query.EQ("ParentId", current.Id), Query.EQ("Name", item)));
                if (current == null) return null;
            }
            return current;
        }

        /// <summary>
        /// ディレクトリ名から、対応するDirectoryDbEntryを見つける
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        public DirectoryDbEntry GetDirectryFromDirPath(string dirPath)
        {
            var paths = dirPath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            var qname = paths[0];
            var current = _directoryDbEntries.FindOne(m => m.ParentId == -1 && m.Name == qname);
            if (current == null) return null;

            // ドライブ以降の各要素を辿る。"C:\" のような末尾区切りで生じる空要素はスキップする。
            // (ファイルパス版と異なり、末尾の要素もディレクトリ名なので取り除いてはいけない)
            foreach (var item in paths.Skip(1))
            {
                if (string.IsNullOrEmpty(item)) continue;
                var parentId = current.Id;
                current = _directoryDbEntries.FindOne(m => m.ParentId == parentId && m.Name == item);
                if (current == null) return null;
            }
            return current;
        }

        object _deleteCreateDirectryLock = new object();
        DirectoryDbEntry CreateDirectryIfNotExists(string fullPath)
        {
            lock (_deleteCreateDirectryLock)
            {
                var paths = fullPath.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                var qname = paths[0];
                var current = _directoryDbEntries.FindOne(m => m.ParentId == -1 && m.Name == qname);
                //var current = _directoryDbEntries.FindOne(Query.And(Query.EQ("ParentId", -1), Query.EQ("Name", paths[0])));
                if (current == null)
                {
                    current = new DirectoryDbEntry
                    {
                        Name = paths[0],
                        ParentId = -1,
                    };
                    _directoryDbEntries.Insert(current);
                }

                // "C:\file.ext"
                if (paths.Length == 2) return current;

                foreach (var item in paths.Skip(1).Take(paths.Length - 2))
                {
                    var parentId = current.Id;
                    current = _directoryDbEntries.FindOne(m => m.ParentId == parentId && m.Name == item);
                    //current = _directoryDbEntries.FindOne(Query.And(Query.EQ("ParentId", parentId), Query.EQ("Name", item)));
                    if (current == null)
                    {
                        current = new DirectoryDbEntry
                        {
                            Name = item,
                            ParentId = parentId,
                        };
                        _directoryDbEntries.Insert(current);
                    }
                }
                return current;
            }
        }

        /// <summary>
        /// バックアップファイル名生成
        /// </summary>
        /// <param name="DataDir"></param>
        /// <param name="OriginalFullPath"></param>
        /// <param name="BackupTime"></param>
        /// <returns></returns>
        public static string BackupFileName(string DataDir, string OriginalFullPath, DateTime BackupTime)
        {
            return Path.Combine(DataDir, OriginalFullPath.Split(':')[0], Path.GetDirectoryName(OriginalFullPath).Split(':')[1].Substring(1),
                        $"{Path.GetFileNameWithoutExtension(OriginalFullPath)}({BackupTime:yyyy_MM_dd HH_mm_ss}){Path.GetExtension(OriginalFullPath)}");
        }

        public string GetFileDir(int fileId)
        {
            var fileentry = _fileDbEntries.FindById(fileId);
            var directoryId = fileentry.DirectoryId;
            var ret = "";
            while (directoryId != -1)
            {
                var dirEentry = _directoryDbEntries.FindById(directoryId);
                directoryId = dirEentry.ParentId;
                ret = Path.Combine(dirEentry.Name, ret);
            }
            return ret;
        }

        /// <summary>
        /// 全てのFileDbEntryを取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<FileDbEntry> FindAllFiles()
        {
            return _fileDbEntries.FindAll();
        }

        public void Dispose()
        {
            try
            {
                _db.Dispose();
            }
            catch (Exception) { }
        }
    }


    public class AttributeBase
    {
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public long Size { get; set; }

        public DateTime LastUpdate
        {
            get
            {
                return CreationTime > LastWriteTime ? CreationTime : LastWriteTime;
            }
        }
    }

    public class AttributeDbEntry : AttributeBase
    {
        [BsonId]
        public int Id { get; set; }
        public int FileId { get; set; }
        public DateTime BackupTime { get; set; }

        public AttributeDbEntry() { }
        public AttributeDbEntry(AttributeFileEntry fileEntry)
        {
            this.CreationTime = fileEntry.CreationTime;
            this.LastWriteTime = fileEntry.LastWriteTime;
            this.LastAccessTime = fileEntry.LastAccessTime;
            this.Size = fileEntry.Size;
        }
    }

    public class AttributeFileEntry : AttributeBase
    {
        public bool IsValid { get; private set; }
        public AttributeFileEntry(string file)
        {
            if (File.Exists(file))
            {
                CreationTime = File.GetCreationTime(file);
                LastWriteTime = File.GetLastWriteTime(file);
                LastAccessTime = File.GetLastAccessTime(file);
                Size = new FileInfo(file).Length;
                IsValid = true;
            }
            else
                IsValid = false;
        }
    }

    public class FileDbEntry
    {
        [BsonId]
        public int Id { get; set; }
        public int DirectoryId { get; set; }
        public string Name { get; set; }
    }

    public class DirectoryDbEntry
    {
        [BsonId]
        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
    }
}
