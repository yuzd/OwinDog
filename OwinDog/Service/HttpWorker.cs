using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OwinEngine;
using Util;

namespace Service
{

    public sealed class HttpWorker
    {
        private static readonly string _htmlRoot = ApplicationInfo.Wwwroot;


        private static readonly string[] _indexFiles = new string[]
        {
            "index.html",
            "Index.html",
            "default.html",
            "Default.html",
            "index.htm",
            "Index.htm",
            "default.htm",
            "Default.htm"
        };

        private static readonly char[] SlashChar = new char[]
        {
            '/',
            '~',
            '\\'
        };

        private static readonly ConcurrentDictionary<string, bool> _NoFileRequest = new ConcurrentDictionary<string, bool>();


        /// <summary>
        /// 返回请求地址对于磁盘的地址 如果没有 拿默认的Index 页面
        /// </summary>
        /// <param name="urlPath"></param>
        /// <returns></returns>
        private static string GetFileFullPath(string urlPath)
        {
            if (urlPath.IndexOf('%') > -1)
            {
                urlPath = UrlDeCode.Decode(urlPath, Encoding.UTF8);
            }
            urlPath = urlPath.TrimStart(SlashChar);
            string path = Path.Combine(_htmlRoot, urlPath);
            path = Path.GetFullPath(path);
            if (!path.StartsWith(_htmlRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (File.Exists(path))
            {
                return path;
            }
            if (Directory.Exists(path))
            {
                string[] a = _indexFiles;
                for (int i = 0; i < a.Length; i++)
                {
                    string text4 = a[i];
                    string text3 = Path.Combine(path, text4);
                    if (File.Exists(text3))
                    {
                        return text3;
                    }
                }
            }
            return path;
        }

        public bool Process(RequestData requestData)
        {
            if (_NoFileRequest.ContainsKey(requestData.RequestUrl))
            {
                //不存在的请求文件
                return false;
            }
            CacheManager.Cache cache = CacheManager.GetCache(requestData.RequestUrl);
            if (cache != null && cache.FileBytes != null && cache.FileBytes.Length > 0)
            {
                //已有缓存
                SendCache(requestData, cache);
                return true;
            }
            string fileFullPath = null;
            try
            {
                fileFullPath = GetFileFullPath(requestData.SafeRequestUrl);
            }
            catch
            {
                fileFullPath = null;
            }
            if (string.IsNullOrEmpty(fileFullPath))
            {
                RequestCheck.Expect(requestData.SafeRequestUrl);//排除掉
                EndRequest(requestData.Socket, 400, string.Format("Bad Request:{0}", requestData.SafeRequestUrl));
                requestData.SaveToPoll();
                return true;
            }
            if (!File.Exists(fileFullPath))
            {
                if (!_NoFileRequest.ContainsKey(requestData.RequestUrl))
                {
                    _NoFileRequest[requestData.RequestUrl] = true;
                }
                return false;
            }
            try
            {
                RealProcess(requestData, fileFullPath);
            }
            catch (Exception arg)
            {
                Console.WriteLine("http worker error:{0}", arg);
                requestData.Socket.Dispose();
                requestData.SaveToPoll();
            }
            return true;
        }

        private void RealProcess(RequestData req, string file)
        {
            long writeLastTime = 0;
            long fileLength = 0;
            if (!CommonUtil.GetFileLastWriteTimeAndLength(file, ref writeLastTime, ref fileLength))
            {
                EndRequest(req.Socket, 500, "Can't Open File.");
                req.SaveToPoll();//回收
                return;
            }
            if (fileLength < 131072)
            {
                //一次发送
                SendFile(req, file, writeLastTime, fileLength);
                return;
            }
            //多次发送
            new SendBigFile(req, file, writeLastTime, fileLength).Run();
        }

        /// <summary>
        /// 发送已缓存
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="cache"></param>
        private static void SendCache(RequestData requestData, CacheManager.Cache cache)
        {
            if (String.Compare(requestData.GetEtag(), cache.Key, StringComparison.OrdinalIgnoreCase) == 0 || requestData.IfModifiedSince() == cache.LastWriteTime)
            {
                byte[] array = ResultFactory.BuildCacheResult(cache.Key, cache.LastWriteTime, requestData.IsKeepAlive(), 0);
                requestData.Socket.Write(array, new Action<OwinSocket, int, Exception, object>(SendCompleteCallback), requestData);
                return;
            }
            Tuple<int, int> tuple = requestData.IfRange();
            if (tuple != null)
            {
                int num;
                int num2;
                GetNextSize(tuple, cache.FileBytes.Length, out num, out num2);
                byte[] array2 = new byte[num2];
                Buffer.BlockCopy(cache.FileBytes, num, array2, 0, num2);
                string s2 = ResultFactory.BuildResult(206, cache.HttpMimeType, "", num, checked(num + num2), cache.FileBytes.Length, false, requestData.IsKeepAlive());
                requestData.Socket.WriteForPost(Encoding.ASCII.GetBytes(s2), array2, new Action<OwinSocket, int, Exception, object>(SendCompleteCallback), requestData);
                return;
            }
            byte[] array3 = (cache.FileBytesCompress != null) ? cache.FileBytesCompress : cache.FileBytes;
            byte[] array4 = ResultFactory.BuildSuccessResult(cache.HttpMimeType, array3.Length, cache.Key, cache.LastWriteTime, requestData.IsKeepAlive(), cache.FileBytesCompress != null);
            requestData.Socket.WriteForPost(array4, array3, new Action<OwinSocket, int, Exception, object>(SendCompleteCallback), requestData);
        }

        public static void EndRequest(ISocket socket, int status, string message)
        {
            byte[] array;
            if (status != 400)
            {
                if (status != 404)
                {
                    array = ResultFactory.FormartSE500(message);
                }
                else
                {
                    array = ResultFactory.Formart404(message);
                }
            }
            else
            {
                array = ResultFactory.FormartBR400(message);
            }
          
            socket.Write(array, SendCompleteCallback, null);
        }

        private static void SendFile(RequestData req, string file, long mtime, long fsize)
        {
            byte[] array = null;
            try
            {
                array = File.ReadAllBytes(file);
            }
            catch
            {
                EndRequest(req.Socket, 500, "Can't Open File.");
                req.SaveToPoll();
                req = null;
                return;
            }
            string fileExtention = CommonUtil.GetFileExtention(file);
            string mimeType = HttpMimeTypeManage.GetHttpMimeType(fileExtention);

            Tuple<int, int> tuple = req.IfRange();
            if (tuple == null)
            {
                byte[] array2 = ResultFactory.BuildSuccessResult(mimeType, fsize, CacheManager.FileLastTimeAndFileLengthString(file, fsize, mtime), CommonUtil.GetFileTime(mtime), req.IsKeepAlive(), false);
                req.Socket.WriteForPost(array2, array, new Action<OwinSocket, int, Exception, object>(SendCompleteCallback), req);
                CacheManager.Save(req.RequestUrl, file, array, req.SafeRequestUrl);
                return;
            }

            int num3 = 0;
            int num4 = checked((int)fsize);
            GetNextSize(tuple, fsize, out num3, out num4);
            byte[] array3 = new byte[num4];
            Buffer.BlockCopy(array, num3, array3, 0, num4);
            //206的续传回应
            string s = ResultFactory.BuildResult(206, mimeType, "", num3, checked(num3 + num4), fsize, false, req.IsKeepAlive());
            byte[] bytes = Encoding.ASCII.GetBytes(s);
            req.Socket.WriteForPost(bytes, array3, new Action<OwinSocket, int, Exception, object>(SendCompleteCallback), req);
        }

        private static void SendCompleteCallback(OwinSocket sck, int status, Exception ex, object state)
        {
            if (state == null)
            {
                sck.Dispose();
                return;
            }
            RequestData requestData = (RequestData)state;
            byte[] preLoadedBody = requestData._preLoadedBody;
            bool flag = requestData.IsKeepAlive();
            requestData.SaveToPoll();
            if (status != 0 || ex != null || !flag)
            {
                sck.Dispose();
                return;
            }
            OwinHttpWorkerManage.Start(sck, preLoadedBody);
        }

        private static void GetNextSize(Tuple<int, int> tuple, long num, out int ptr, out int ptr2)
        {
            ptr = 0;
            ptr2 = (int)num;
            if (tuple.Item1 == -1)
            {
                ptr = (int)(num - tuple.Item2);
                ptr2 = tuple.Item2;
                return;
            }
            if (tuple.Item2 == -1)
            {
                ptr = tuple.Item1;
                ptr2 = (int)(num - ptr);
                return;
            }
            ptr = tuple.Item1;
            ptr2 = tuple.Item2 - tuple.Item1;
        }



        private class SendBigFile
        {
            private const int MAXSIZE = 65536;

            private RequestData _req;

            private readonly string _fileName;

            private readonly long _modifyTime;

            private readonly long _fileSize;

            private FileStream _fs;

            private long _sendedSize;

            private long _offsetSize;

            public SendBigFile(RequestData requestData, string file, long mtime, long fsize)
            {
                _req = requestData;
                _fileName = file;
                _modifyTime = mtime;
                _fileSize = fsize;
                _offsetSize = fsize;
            }

            public void Run()
            {
                try
                {
                    _fs = File.OpenRead(_fileName);
                }
                catch
                {
                    EndRequest(_req.Socket, 500, "can't open file.");
                    _req.SaveToPoll();
                    _req = null;
                    return;
                }
                string extention = CommonUtil.GetFileExtention(_fileName);
                string mimeType = HttpMimeTypeManage.GetHttpMimeType(extention);
                Tuple<int, int> tuple = _req.IfRange();
                int num;
                int num2;
                
                if (tuple == null)
                {
                    byte[] headDomain = ResultFactory.BuildSuccessResult(mimeType, _fileSize, "", CommonUtil.GetFileTime(_modifyTime), _req.IsKeepAlive(), false);
                    byte[] body = new byte[32768];
                    _fs.Read(body, 0, body.Length);
                    _sendedSize += body.Length;
                    _offsetSize -= body.Length;
                    _req.Socket.WriteForPost(headDomain, body, new Action<OwinSocket, int, Exception, object>(CompilteCallback), null);
                    return;
                }
                //续传
                GetNextSize(tuple, checked((int)_fileSize), out num, out num2);
                _sendedSize = num;
                _offsetSize = num2;
                string s = ResultFactory.BuildResult(206, mimeType, "", _sendedSize, _offsetSize, _fileSize, false, _req.IsKeepAlive());
                byte[] array3 = (_offsetSize > 32768L) ? new byte[32768] : new byte[_offsetSize];
                _fs.Seek(_sendedSize, SeekOrigin.Begin);
                _fs.Read(array3, (int)_sendedSize, array3.Length);
                _sendedSize += array3.Length;
                _offsetSize -= array3.Length;
                _req.Socket.WriteForPost(Encoding.ASCII.GetBytes(s), array3, new Action<OwinSocket, int, Exception, object>(CompilteCallback), null);
            }

            private void CompilteCallback(OwinSocket sck, int status, Exception ex, object state)
            {
                if (status != 0 || ex != null)
                {
                    _fs.Close();
                    sck.Dispose();
                    _req.SaveToPoll();
                    return;
                }
                if (_offsetSize <= 0)
                {
                    _fs.Close();
                    if (_req.IsKeepAlive())
                    {
                        OwinHttpWorkerManage.Start(sck, _req._preLoadedBody);
                    }
                    else
                    {
                        sck.Dispose();
                    }
                    _req.SaveToPoll();
                    _req = null;
                    return;
                }
                long num2 = _offsetSize;
                if (num2 > 65536)
                {
                    num2 = 65536;
                }
                byte[] array = new byte[num2];
                _fs.Seek(_sendedSize, SeekOrigin.Begin);
                checked
                {
                    _fs.Read(array, 0, (int)num2);
                    _sendedSize += num2;
                    _offsetSize -= num2;
                    sck.Write(array, new Action<OwinSocket, int, Exception, object>(CompilteCallback), null);
                }
            }

        }
    }
}
