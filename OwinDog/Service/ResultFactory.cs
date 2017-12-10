using System;
using System.Text;

namespace Service
{
    public sealed class ResultFactory
    {
        private const string strFORMAT_304 = "{0} 304 Not Modified\r\nDate: {1}\r\n{2}\r\nAccept-Ranges: bytes\r\nLast-Modified: {3}\r\nETag: \"{4}\"\r\n{5}\r\n\r\n";

        private const string SE500 = "HTTP/1.1 500 Server error\r\nContent-Type: text/html\r\nConnection: close\r\n\r\n<html><head><title>500 Server Error</title></head><body><h1>Server error</h1>\r\n{0}\r\n</body></html>\r\n";

        public static byte[] Abyte;

        private const string BR400 = "HTTP/1.1 400 Bad Request\r\nContent-Type: text/html\r\nConnection: close\r\n\r\n<html><head><title>400 Bad Request</title></head><body><h1>Bad Request!</h1>\r\n{0}\r\n</body></html>\r\n";

        private const string Er502 = "HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";

        private const string SE503 = "HTTP/1.1 503 Service unavailable\r\nContent-Type: text/html\r\nConnection: close\r\n\r\n<html><head><title>503 Service unavailable</title></head><body><h2>Service unavailable.</h2>\r\n<hr/>{0}\r\n</body></html>\r\n";

        private const string Str200Test = "HTTP/1.1 200 OK\r\nAccept-Reanges: bytes\r\nContent-Length: {0}\r\nConnection: close\r\n\r\n";

        private const string StrFormat200 = "{0} 200 OK\r\nDate: {1}\r\n{2}\r\nAccept-Ranges: bytes\r\nContent-Type: {3}\r\nContent-Length: {4}\r\nLast-Modified: {5}\r\n{6}\r\n\r\n";

        internal static string WebServerVersion;

        static ResultFactory()
        {
            WebServerVersion = string.Format("Server: {0}", "OwinDog/1.0");
            Abyte = Encoding.ASCII.GetBytes(Er502);
        }

        public static byte[] FormartBR400(string arg)
        {
            string s = string.Format(BR400, arg);
            return Encoding.ASCII.GetBytes(s);
        }

        public static byte[] FormartSE500(string arg)
        {
            string s = string.Format(SE500, arg);
            return Encoding.ASCII.GetBytes(s);
        }

        public static byte[] FormartStr200Test(bool flag)
        {
            string s = string.Format(Str200Test, WebServerVersion, ActionQueue.Time.ToUniversalTime().ToString("r"));
            return Encoding.ASCII.GetBytes(s);
        }
        public static byte[] FormartSE503B(string arg)
        {
            string s = string.Format(SE503, arg);
            return Encoding.ASCII.GetBytes(s);
        }

        public static byte[] Formart404(string arg)
        {
            string s = string.Format("HTTP/1.0 404 Not Found\r\nContent-Type: text/html\r\n" + WebServerVersion + "\r\nConnection: close\r\n\r\n<html><head><title>404 Not Found</title></head>\r\n<body><h1>Can not find:</h1>{0}<p>\r\n</body></html>\r\n", arg);
            return Encoding.ASCII.GetBytes(s);
        }



        public static byte[] BuildCacheResult(string text, DateTime dateTime, bool flag, int num)
        {
            string text2 = ActionQueue.Time.ToUniversalTime().ToString("r");
            string text3;
            if (flag)
            {
                text3 = "Keep-Alive: timeout=30, max=" + num + "\r\n";
                text3 += "Connection: Keep-Alive";
            }
            else
            {
                text3 = "Connection: close";
            }
            string s = string.Format(strFORMAT_304, new object[]
            {
                "HTTP/1.1",
                text2,
                WebServerVersion,
                dateTime.ToString("r"),
                text,
                text3
            });
            return Encoding.ASCII.GetBytes(s);
        }

        public static byte[] BuildSuccessResult(string contentType, long contentLength, string eTag, DateTime lastModifyTime, bool isKeepAlive, bool isGzip)
        {
            return BuildSuccessResult(contentType, contentLength, eTag, lastModifyTime, 0, isKeepAlive, isGzip);
        }

        private static byte[] BuildSuccessResult(string contentType, long contentLength, string eTag, DateTime lastModifyTime, int num2, bool isKeepAlive, bool isGzip)
        {
            string timeNow = ActionQueue.Time.ToUniversalTime().ToString("r");
            string lastModifyTimeStr = lastModifyTime.ToString("r");
            string ext = string.IsNullOrEmpty(eTag) ? "" : string.Format("ETag: {0}\r\n", eTag);
            if (num2 > 0)
            {
                ext = ext + "Cache-Control: max-age=" + num2.ToString() + "\r\n";
            }
            if (isKeepAlive)
            {
                ext = string.Concat(new string[]
                {
                    ext,
                    "Keep-Alive: timeout=",
                    "30",
                    "\r\nConnection: Keep-Alive"
                });
            }
            else
            {
                ext += "Connection: close";
            }
            if (isGzip)
            {
                contentType += "\r\nContent-Encoding: gzip";
            }
            string s = string.Format(StrFormat200, new object[]
            {
                "HTTP/1.1",
                timeNow,
                WebServerVersion,
                contentType,
                contentLength.ToString(),
                lastModifyTimeStr,
                ext
            });
            return Encoding.ASCII.GetBytes(s);
        }

        public static string BuildResult(int num, string str, string str2, long num2, long num3, long num4, bool noCache, bool isKeepAlive)
        {
            string str3 = ActionQueue.Time.ToUniversalTime().ToString("r");
            string str4;
            if (isKeepAlive)
            {
                str4 = "Connection: Keep-Alive";
            }
            else
            {
                str4 = "Connection: close";
            }
            StringBuilder stringBuilder = new StringBuilder();

            if (num == 200)
            {
                stringBuilder.Append("HTTP/1.1 200 OK\r\n");
                stringBuilder.Append("Date: " + str3 + "\r\n");
                stringBuilder.Append(WebServerVersion + "\r\n");
                stringBuilder.Append("Accept-Ranges: bytes\r\n");
                stringBuilder.Append("Content-Type: " + str + "\r\n");
                stringBuilder.Append("Content-Length: " + (num3 - num2 + 1L).ToString() + "\r\n");
                if (str2 != "")
                {
                    stringBuilder.Append("Set-Cookie: " + str2 + "\r\n");
                }
                if (noCache)
                {
                    stringBuilder.Append("pragma: no-cache\r\n");
                    stringBuilder.Append("Cache-Control: no-cache\r\n");
                }
                else
                {
                    stringBuilder.Append("Cache-Control: private, max-age=60\r\n");
                }
                stringBuilder.Append(str4 + "\r\n");
                stringBuilder.Append("\r\n");
                return stringBuilder.ToString();
            }
            if (num == 206)
            {
                stringBuilder.Append("HTTP/1.1 206 Partial content\r\n");
                stringBuilder.Append("Date: " + str3 + "\r\n");
                stringBuilder.Append(WebServerVersion + "\r\n");
                stringBuilder.Append("Accept-Ranges: bytes\r\n");
                stringBuilder.Append("Content-Type: " + str + "\r\n");
                stringBuilder.Append("Content-Length: " + (num3 - num2 + 1L).ToString() + "\r\n");
                stringBuilder.Append(string.Concat(new string[]
                {
                        "Content-Range: bytes ",
                        num2.ToString(),
                        "-",
                        num3.ToString(),
                        "/",
                        num4.ToString(),
                        "\r\n"
                }));
                if (str2 != "")
                {
                    stringBuilder.Append("Set-Cookie: " + str2 + "\r\n");
                }
                if (noCache)
                {
                    stringBuilder.Append("pragma: no-cache\r\n");
                    stringBuilder.Append("Cache-Control: no-cache\r\n");
                }
                else
                {
                    stringBuilder.Append("Cache-Control: private\r\n");
                }
                stringBuilder.Append(str4 + "\r\n");
                stringBuilder.Append("\r\n");
                return stringBuilder.ToString();
            }
            if (num == 400)
            {
                return BR400;
            }
            return "";
        }




    }
}
