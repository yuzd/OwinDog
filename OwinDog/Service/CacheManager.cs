using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Util;

namespace Service
{
    //平时不要把ReaderWriterLockSlim的实例放到try{}块中，以减少性能损耗
    public static class CacheManager
    {
        private const int FileSaveMaxTime = 60;

        private const int BuffMaxFiles = 5000;

        private const int BuffMaxMemory = 134217728;

        private const int MinFileSize_GZIP = 4096;


        private static readonly Dictionary<string, Cache> _arrayCacheTable;

        private static readonly ReaderWriterLockSlim _mainLock;

        private static readonly List<string> _zipFileExtList;

        private static readonly List<string> _waitClearChanagedFiles;

        private static long _lastCheckTimeoutTimeSec;

        private static int _nowUsedMemory;

        private static int _CheckFileChanged;

        private static int _addingLockTag;
        static CacheManager()
        {
            _arrayCacheTable = new Dictionary<string, Cache>(BuffMaxFiles);
            _mainLock = new ReaderWriterLockSlim();
            _nowUsedMemory = 0;
            _CheckFileChanged = 0;
            _waitClearChanagedFiles = new List<string>();
            _addingLockTag = 0;
            string[] array = new string[]
            {
                "txt",
                "htm",
                "html",
                "js",
                "css",
                "xml",
                "c",
                "cs",
                "json",
                "vbs",
                "ppt",
                "pdf",
                "doc",
                "xls",
                "htc",
                "docx",
                "xsl"
            };
            _zipFileExtList = array.ToList();
            ActionQueue.AddAction(new Action(FreeCache), 2500);
        }

        public static void FreeCache()
        {
            if (Interlocked.CompareExchange(ref _CheckFileChanged, 1, 0) == 1)
            {
                return;
            }
            if (!_mainLock.TryEnterWriteLock(100))//尝试进入读取模式锁定状态
            {
                Interlocked.Exchange(ref _CheckFileChanged, 0);
                return;
            }
            try
            {
                if (_waitClearChanagedFiles.Count >= 1)
                {
                    foreach (string current in _waitClearChanagedFiles)
                    {
                        try
                        {
                            FreeOne(current);
                        }
                        catch (Exception)
                        {
                            //ignore
                        }
                    }
                    _waitClearChanagedFiles.Clear();
                }
            }
            catch
            {
                //ignore
            }
            finally
            {
                Interlocked.Exchange(ref _CheckFileChanged, 0);
                _mainLock.ExitWriteLock();
            }
        }
        public static void CheckTimeOut()
        {
            if ((ActionQueue.LongTimes / 1000 - _lastCheckTimeoutTimeSec) < 180L)
            {
                return;
            }
            _lastCheckTimeoutTimeSec = ActionQueue.LongTimes / 1000;
            if (!_mainLock.TryEnterWriteLock(500))
            {
                return;
            }
            try
            {
                RealCheckTimeOut();
            }
            finally
            {
                _mainLock.ExitWriteLock();
            }
        }

        private static int HexStrToNum(string text)
        {
            int num = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                checked
                {
                    if (c > '/' && c < ':')
                    {
                        num += (int)(c - '0');
                    }
                    else if (c > '@' && c < 'G')
                    {
                        num += (int)(c - '7');
                    }
                    else if (c > '`' && c < 'g')
                    {
                        num += (int)(c - 'W');
                    }
                }
            }
            return num;
        }

        public static bool HasCached(string key)
        {
            if (!_mainLock.TryEnterReadLock(100))
            {
                return false;
            }
            bool result;
            try
            {
                result = _arrayCacheTable.ContainsKey(key);
            }
            finally
            {
                _mainLock.ExitReadLock();
            }
            return result;
        }

        public static string FileLastTimeAndFileLengthString(string text, long num, long num2)
        {
            return string.Format("{0}-{1}-{2}", text.GetHashCode().ToString("x"), num.ToString("x"), num2.ToString("x"));
        }

        public static void Save(string key, string path, byte[] array, string safeRequestUrl)
        {
            if (Interlocked.CompareExchange(ref _addingLockTag, 1, 0) != 0)
            {
                return;
            }
            if (HasCached(key))
            {
                Interlocked.Exchange(ref _addingLockTag, 0);
                return;
            }
            if (!_mainLock.TryEnterWriteLock(500))
            {
                Interlocked.Exchange(ref _addingLockTag, 0);
                return;
            }

            try
            {
                if (_nowUsedMemory <= BuffMaxMemory)
                {
                    if (_arrayCacheTable.Count <= BuffMaxFiles)
                    {
                        long lastWriteTime = 0;
                        long fileLength = 0;
                        if (CommonUtil.GetFileLastWriteTimeAndLength(path, ref lastWriteTime, ref fileLength))
                        {
                            string cacheKey = FileLastTimeAndFileLengthString(path, fileLength, lastWriteTime);
                            if (array == null)
                            {
                                array = File.ReadAllBytes(path);
                            }
                            Cache cache = new Cache();
                            cache.FileBytes = array;
                            string safePath = CommonUtil.GetFileExtention(path);
                            if (!string.IsNullOrEmpty(safePath))
                            {
                                safePath = safePath.ToLower();
                            }
                            if (array.Length >= MinFileSize_GZIP && IsInzipFileExtList(safePath))
                            {
                                try
                                {
                                    cache.FileBytesCompress = CommonUtil.Compress(array);
                                }
                                catch
                                {
                                    cache.FileBytesCompress = null;
                                }
                            }
                            cache.LastWriteTime = CommonUtil.GetFileTime(lastWriteTime);
                            cache.LastWriteTimeLong = lastWriteTime;
                            cache.Times = ActionQueue.LongTimes / 1000L;
                            cache.Path = path;
                            cache.Key = cacheKey;
                            cache.HttpMimeType = HttpMimeTypeManage.GetHttpMimeType(safePath);
                            cache.RequestUrl = safeRequestUrl;
                            _arrayCacheTable.Add(key, cache);
                            _nowUsedMemory += array.Length;
                        }
                    }
                }
            }
            catch
            {
                //ignore
            }
            finally
            {
                _mainLock.ExitWriteLock();
                Interlocked.Exchange(ref _addingLockTag, 0);
            }
        }

        private static void RealCheckTimeOut()
        {
            if (_arrayCacheTable.Count < 1)
            {
                return;
            }
            long num = ActionQueue.LongTimes / 1000;
            List<string> list = new List<string>();
            Dictionary<string, Cache>.KeyCollection keys = _arrayCacheTable.Keys;
            long lastWriteTime = 0;
            long fileLength = 0;
            foreach (string current in keys)
            {
                Cache cache;
                if (!string.IsNullOrEmpty(current) && (cache = _arrayCacheTable[current]) != null)
                {
                    if (!CommonUtil.GetFileLastWriteTimeAndLength(cache.Path, ref fileLength, ref lastWriteTime))
                    {
                        list.Add(current);
                    }
                    else if (fileLength != cache.LastWriteTimeLong)
                    {
                        list.Add(current);
                    }
                    else if ((num - cache.Times) >= 60L)
                    {
                        list.Add(current);
                    }
                }
            }
            if (list.Count <= 0)
            {
                return;
            }
            foreach (string current2 in list)
            {
                try
                {
                    FreeOne(current2);
                }
                catch
                {
                    //ignore
                }
            }
        }

        public static int CacheCount()
        {
            return _arrayCacheTable.Keys.Count;
        }

        public static Cache GetCache(string key)
        {
            if (!_mainLock.TryEnterReadLock(FileSaveMaxTime))
            {
                return null;
            }
            Cache result;
            try
            {
                if (!_arrayCacheTable.Keys.Contains(key))
                {
                    result = null;
                }
                else
                {
                    Cache cache = _arrayCacheTable[key];
                    if (cache == null || cache.FileBytes == null)
                    {
                        result = null;
                    }
                    else
                    {
                        if (ActionQueue.LongTimes / 1000 - cache.Times > 3)
                        {
                            cache.Times = ActionQueue.LongTimes / 1000;
                            if (Interlocked.CompareExchange(ref _CheckFileChanged, 1, 0) == 0)
                            {
                                long num = 0L;
                                long num2 = 0L;
                                bool flag = false;
                                if (CommonUtil.GetFileLastWriteTimeAndLength(cache.Path, ref num, ref num2))
                                {
                                    if (cache.LastWriteTimeLong != num)
                                    {
                                        flag = true;
                                    }
                                }
                                else
                                {
                                    flag = true;
                                }
                                if (flag && !_waitClearChanagedFiles.Contains(key))
                                {
                                    _waitClearChanagedFiles.Add(key);
                                }
                                Interlocked.Exchange(ref _CheckFileChanged, 0);
                            }
                        }
                        result = cache;
                    }
                }
            }
            catch
            {
                result = null;
            }
            finally
            {
                _mainLock.ExitReadLock();
            }
            return result;
        }

        private static void FreeOne(string key)
        {
            Cache cache = _arrayCacheTable[key];
            if (cache == null)
            {
                return;
            }
            int length = (cache.FileBytes == null) ? 0 : cache.FileBytes.Length;
            _arrayCacheTable[key] = null;
            _arrayCacheTable.Remove(key);

            _nowUsedMemory -= length;
            if (_nowUsedMemory < 0)
            {
                _nowUsedMemory = 0;
            }
        }

        public static string FileLastTimeAndFileLengthString(string path)
        {
            long lastWriteTime = 0;
            long fileLength = 0;
            if (!CommonUtil.GetFileLastWriteTimeAndLength(path, ref lastWriteTime, ref fileLength))
            {
                return "";
            }
            return FileLastTimeAndFileLengthString(path, fileLength, lastWriteTime);
        }

        public static bool IsInzipFileExtList(string text)
        {
            return !string.IsNullOrEmpty(text) && _zipFileExtList.Contains(text.ToLower());
        }



        public sealed class Cache
        {
            public string Path = "";

            public byte[] FileBytes;

            public byte[] FileBytesCompress;

            public DateTime LastWriteTime;

            public long LastWriteTimeLong;

            public long Times;

            public string Key = "";

            public string HttpMimeType = "";

            public string RequestUrl = "";
        }
    }
}
