namespace Owin.AspEngine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Web;
    using System.Web.Hosting;

    internal sealed class AspRequestWorker : SimpleWorkerRequest
    {
        private static string _defaultDoc = "";
        private HttpWorkerRequest.EndOfSendNotification _endSendCbFunc;
        private object _endSendData;
        private bool _isConnected;
        private bool _isRequestCloseed;
        private bool _keepAlive;
        private AspRequestData _reqData;
        private string[][] _unknownHeaders;
        private static AspApplicationHost AppHost;
        private static AspRequestBroker ReqBroker;

        public AspRequestWorker() : base(string.Empty, string.Empty, null)
        {
            this._keepAlive = true;
            this._isConnected = true;
        }

        public override void CloseConnection()
        {
            if (!this._isRequestCloseed)
            {
                this._isRequestCloseed = true;
                this._isConnected = false;
                ReqBroker.RequestEnd(this._reqData.RequestId, this._keepAlive);
            }
        }

        public override void EndOfRequest()
        {
            this.CloseConnection();
            if (this._endSendCbFunc != null)
            {
                this._endSendCbFunc(this, this._endSendData);
            }
        }

        public override void FlushResponse(bool finalFlush)
        {
            try
            {
                if (finalFlush)
                {
                    this.CloseConnection();
                }
            }
            catch (Exception)
            {
                this._keepAlive = false;
                throw;
            }
        }

        public override string GetAppPath() => 
            AppHost.VPath;

        public override string GetAppPathTranslated() => 
            AppHost.Path;

        public override string GetFilePath()
        {
            if (this._reqData.UrlPath == "/")
            {
                return (this._reqData.UrlPath + _defaultDoc);
            }
            return this._reqData.UrlPath;
        }

        public override string GetFilePathTranslated()
        {
            string str = this.GetFilePath().TrimStart(new char[] { '/', '\\' });
            if (string.IsNullOrEmpty(str))
            {
                return AppHost.Path;
            }
            string fullPath = Path.GetFullPath(Path.Combine(AppHost.Path, str));
            if (!fullPath.StartsWith(AppHost.Path))
            {
                return AppHost.Path;
            }
            return fullPath;
        }

        public override string GetHttpVerbName() => 
            this._reqData.Verb;

        public override string GetHttpVersion() => 
            this._reqData.Protocol;

        public override string GetKnownRequestHeader(int index)
        {
            if ((this._reqData.RequestHttpHeader == null) || (this._reqData.RequestHttpHeader.Count < 1))
            {
                return "";
            }
            string knownRequestHeaderName = HttpWorkerRequest.GetKnownRequestHeaderName(index);
            return this.GetUnknownRequestHeader(knownRequestHeaderName);
        }

        public override string GetLocalAddress() => 
            this._reqData.LocalAddress;

        public override int GetLocalPort() => 
            this._reqData.LocalPort;

        public override string GetPathInfo() => 
            "";

        public override byte[] GetPreloadedEntityBody()
        {
            byte[] buffer = new byte[0x2000];
            int newSize = ReqBroker.Read(this._reqData.RequestId, buffer, 0, buffer.Length);
            if (newSize < 1)
            {
                return null;
            }
            if (newSize < buffer.Length)
            {
                Array.Resize<byte>(ref buffer, newSize);
            }
            return buffer;
        }

        public override string GetQueryString() => 
            this._reqData.QueryString;

        public override byte[] GetQueryStringRawBytes()
        {
            if (string.IsNullOrEmpty(this._reqData.QueryString))
            {
                return null;
            }
            return Encoding.ASCII.GetBytes(this._reqData.QueryString);
        }

        public override string GetRawUrl()
        {
            string urlPath = this._reqData.UrlPath;
            if (!string.IsNullOrEmpty(this._reqData.QueryString))
            {
                return (urlPath + "?" + this._reqData.QueryString);
            }
            return urlPath;
        }

        public override string GetRemoteAddress() => 
            this._reqData.RemoteAddress;

        public override string GetRemoteName() => 
            this.GetRemoteAddress();

        public override int GetRemotePort() => 
            this._reqData.RemotePort;

        public override string GetServerName()
        {
            string knownRequestHeader = this.GetKnownRequestHeader(0x1c);
            if (string.IsNullOrEmpty(knownRequestHeader))
            {
                return this.GetLocalAddress();
            }
            int index = knownRequestHeader.IndexOf(':');
            if (index > 0)
            {
                return knownRequestHeader.Substring(0, index);
            }
            if (index == 0)
            {
                knownRequestHeader = this.GetLocalAddress();
            }
            return knownRequestHeader;
        }

        public override string GetServerVariable(string name)
        {
            string[] strArray;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            switch (name)
            {
                case "GATEWAY_INTERFACE":
                    return "CGI/1.1";

                case "HTTPS":
                    if (!this.IsSecure())
                    {
                        return "off";
                    }
                    return "on";

                case "SERVER_SOFTWARE":
                    return "owindog";
            }
            if (this._reqData.RequestHttpHeader == null)
            {
                return string.Empty;
            }
            if (this._reqData.RequestHttpHeader.TryGetValue(name, out strArray) && ((strArray != null) && (strArray.Length > 1)))
            {
                return string.Join(";", strArray);
            }
            return "";
        }

        public override string GetUnknownRequestHeader(string name)
        {
            string[] strArray;
            if ((this._reqData.RequestHttpHeader == null) || (this._reqData.RequestHttpHeader.Count < 1))
            {
                return "";
            }
            if (!this._reqData.RequestHttpHeader.TryGetValue(name, out strArray))
            {
                return "";
            }
            if ((strArray != null) && (strArray.Length >= 1))
            {
                return string.Join(";", strArray);
            }
            return null;
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            if (this._unknownHeaders == null)
            {
                if ((this._reqData.RequestHttpHeader == null) || (this._reqData.RequestHttpHeader.Count < 1))
                {
                    return (this._unknownHeaders = new string[0][]);
                }
                List<string[]> list = new List<string[]>();
                foreach (KeyValuePair<string, string[]> pair in this._reqData.RequestHttpHeader)
                {
                    string key = pair.Key;
                    if (HttpWorkerRequest.GetKnownRequestHeaderIndex(key) == -1)
                    {
                        string[] strArray = pair.Value;
                        if (strArray != null)
                        {
                            foreach (string str2 in strArray)
                            {
                                list.Add(new string[] { key, str2 });
                            }
                        }
                    }
                }
                if (list.Count != 0)
                {
                    int num2 = 0;
                    this._unknownHeaders = new string[list.Count][];
                    foreach (string[] strArray2 in list)
                    {
                        this._unknownHeaders[num2++] = strArray2;
                    }
                }
            }
            return this._unknownHeaders;
        }

        public override string GetUriPath() => 
            this._reqData.UrlPath;

        public override bool HeadersSent() => 
            true;

        public static void Init(AspApplicationHost host, AspRequestBroker reqBroker)
        {
            AppHost = host;
            ReqBroker = reqBroker;
            string[] fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?efault.aspx");
            if ((fileSystemEntries == null) || (fileSystemEntries.Length < 1))
            {
                fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?ndex.aspx");
            }
            if ((fileSystemEntries == null) || (fileSystemEntries.Length < 1))
            {
                fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?efault.cshtml");
            }
            if ((fileSystemEntries == null) || (fileSystemEntries.Length < 1))
            {
                fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?ndex.cshtml");
            }
            if ((fileSystemEntries == null) || (fileSystemEntries.Length < 1))
            {
                fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?ndex.html");
            }
            if ((fileSystemEntries == null) || (fileSystemEntries.Length < 1))
            {
                fileSystemEntries = Directory.GetFileSystemEntries(AppHost.Path, "?ndex.htm");
            }
            if ((fileSystemEntries != null) && (fileSystemEntries.Length > 0))
            {
                _defaultDoc = Path.GetFileName(fileSystemEntries[0]);
            }
        }

        public override bool IsClientConnected() => 
            this._isConnected;

        public override bool IsEntireEntityBodyIsPreloaded() => 
            true;

        public override bool IsSecure() => 
            false;

        public override string MapPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return AppHost.Path;
            }
            if (string.IsNullOrEmpty(path) || (!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                path = path.TrimStart(new char[] { '~' });
                path = path.TrimStart(new char[] { '/' }).TrimStart(new char[] { '\\' });
                path = Path.Combine(AppHost.Path, path);
                path = Path.GetFullPath(path);
                if (!path.StartsWith(AppHost.Path))
                {
                    throw new Exception();
                }
            }
            return path;
        }

        public void ProcessRequest(AspRequestData req)
        {
            this._reqData = req;
            string s = "";
            bool flag = false;
            try
            {
                HttpRuntime.ProcessRequest(this);
            }
            catch (HttpException exception)
            {
                flag = true;
                s = exception.GetHtmlErrorMessage();
            }
            catch (Exception exception2)
            {
                flag = true;
                s = new HttpException(400, "Bad request", exception2).GetHtmlErrorMessage();
            }
            if (flag)
            {
                this.SendStatus(400, "Bad request");
                this.SendUnknownResponseHeader("Connection", "close");
                this.SendUnknownResponseHeader("Date", DateTime.Now.ToUniversalTime().ToString("r"));
                Encoding encoding = Encoding.UTF8;
                byte[] bytes = encoding.GetBytes(s);
                this.SendUnknownResponseHeader("Content-Type", "text/html; charset=" + encoding.WebName);
                this.SendUnknownResponseHeader("Content-Length", bytes.Length.ToString());
                this._keepAlive = false;
                this.SendResponseFromMemory(bytes, bytes.Length);
                this.FlushResponse(true);
            }
        }

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            int num2;
            if (((this._reqData.Verb == "GET") || (this._reqData.Verb == "HEAD")) || ((this._reqData.Verb == "OPTIONS") || (size == 0)))
            {
                return 0;
            }
            try
            {
                int num = ReqBroker.Read(this._reqData.RequestId, buffer, 0, size);
                if (num < 1)
                {
                    this._isConnected = false;
                }
                num2 = num;
            }
            catch (Exception)
            {
                this._isConnected = false;
                throw;
            }
            return num2;
        }

        public override void SendCalculatedContentLength(int contentLength)
        {
            this.SendUnknownResponseHeader("Content-Length", contentLength.ToString());
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            string knownResponseHeaderName = HttpWorkerRequest.GetKnownResponseHeaderName(index);
            if (!string.IsNullOrEmpty(knownResponseHeaderName))
            {
                this.SendUnknownResponseHeader(knownResponseHeaderName, value);
            }
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            if (!this._isConnected)
            {
                throw new Exception();
            }
#pragma warning disable CS0618 // 类型或成员已过时
            using (FileStream stream = new FileStream(handle, FileAccess.Read))
#pragma warning restore CS0618 // 类型或成员已过时
            {
                byte[] buffer = new byte[length];
                stream.Seek(offset, SeekOrigin.Begin);
                int num = stream.Read(buffer, 0, (int) length);
                this.SendResponseFromMemory(buffer, num);
            }
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            using (FileStream stream = File.OpenRead(filename))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                stream.Read(buffer, 0, (int) length);
                this.SendResponseFromMemory(buffer, (int) length);
            }
        }

        public override void SendResponseFromMemory(IntPtr data, int length)
        {
            if (((data == IntPtr.Zero) || (length < 1)) || !this._isConnected)
            {
                throw new Exception();
            }
            byte[] destination = new byte[length];
            Marshal.Copy(data, destination, 0, length);
            ReqBroker.Write(this._reqData.RequestId, destination, 0, length);
        }

        public override unsafe void SendResponseFromMemory(byte[] data, int length)
        {
            if (((data != null) && (data.Length >= 1)) && (length >= 1))
            {
                fixed (byte* numRef = data)
                {
                    this.SendResponseFromMemory((IntPtr) numRef, length);
                }
            }
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            ReqBroker.WriteStatus(this._reqData.RequestId, statusCode, statusDescription);
            if ((statusCode == 400) || (statusCode >= 500))
            {
                this._keepAlive = false;
                this.SendUnknownResponseHeader("Connection", "close");
            }
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                if ((string.Compare(name, "connection", true, CultureInfo.InvariantCulture) == 0) && (value.ToLower() == "close"))
                {
                    this._keepAlive = false;
                }
                ReqBroker.WriteHeader(this._reqData.RequestId, name, value);
            }
        }

        public override void SetEndOfSendNotification(HttpWorkerRequest.EndOfSendNotification callback, object extraData)
        {
            this._endSendCbFunc = callback;
            this._endSendData = extraData;
        }
    }
}

