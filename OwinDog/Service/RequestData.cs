using System;
using System.Collections.Generic;
using System.Linq;
using OwinEngine;

namespace Service
{
    public sealed class RequestData
    {
        private const int PollSize = 1024;

        private static readonly RequestData[] _reqBuffer = new RequestData[PollSize];

        private static readonly Queue<int> _keys = new Queue<int>(PollSize);

        public string HttpMethod = "";

        public ISocket Socket;

        public string RequestUrl = "";

        public int Port;

        public IDictionary<string, string[]> HeadDomainDic;

        public byte[] _preLoadedBody;

        public string SafeRequestUrl = "";

        public string RequestQueryString = "";

        /// <summary>
        /// Http协议版本
        /// </summary>
        public string HttpVersion = "";

        public string Host = "";

        private RequestData()
        {
        }

        public static RequestData New()
        {
            RequestData result;
            lock (_keys)
            {
                if (_keys.Count < 1)
                {
                    result = new RequestData();
                }
                else
                {
                    int num = _keys.Dequeue();
                    if (_reqBuffer[num] == null)
                    {
                        result = new RequestData();
                    }
                    else
                    {
                        RequestData reqData = _reqBuffer[num];
                        _reqBuffer[num] = null;
                        result = reqData;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// http://www.zhihu.com/question/34074946/answer/108588042
        /// </summary>
        /// <returns></returns>
        public bool IsKeepAlive()
        {
            // 区别：
            //1，HTTP / 1.0协议使用非持久连接,即在非持久连接下,一个tcp连接只传输一个Web对象,；
            //2，HTTP / 1.1默认使用持久连接(然而, HTTP / 1.1协议的客户机和服务器可以配置成使用非持久连接)。
            //在持久连接下,不必为每个Web对象的传送建立一个新的连接,一个连接中可以传输多个对象!
            //到http1.1之后Connection的默认值就是Keep-Alive，如果要关闭连接复用需要显式的设置Connection:Close。
            bool flag = HttpVersion != "HTTP/1.0";
            return flag & Get("Connection") != "close";
        }

        public bool GetCanGzip()
        {
            string text = Get("Accept-Encoding");
            return !string.IsNullOrEmpty(text) && GetCanGzip(text);
        }

        public string GetEtag()
        {
            if (HeadDomainDic == null)
            {
                return "";
            }
            //If - None - Match和ETag一起工作，工作原理是在HTTP Response中添加ETag信息。 当用户再次请求该资源时，将在HTTP Request 中加入If - None - Match信息(ETag的值)。如果服务器验证资源的ETag没有改变（该资源没有更新），将返回一个304状态告诉客户端使用本地缓存文件。否则将返回200状态和新的资源和Etag.使用这样的机制将提高网站的性能
            if (HeadDomainDic.ContainsKey("If-None-Match"))
            {
                return ParseETag(HeadDomainDic["If-None-Match"].FirstOrDefault<string>());
            }
            if (HeadDomainDic.ContainsKey("If-Range"))
            {
                return ParseETag(HeadDomainDic["If-Range"].FirstOrDefault<string>());
            }
            return "";
        }


        /// <summary>
        ///如果请求的对象在该头部指定的时间之后修改了，才执行请求的动作（比如返回对象），否则返回代码304，告诉浏览器该对象没有修改。
        /// </summary>
        /// <returns></returns>
        public DateTime IfModifiedSince()
        {
            if (!HeadDomainDic.ContainsKey("If-Modified-Since"))
            {
                return DateTime.FromFileTime(0L);
            }
            DateTime dateTime;
            if (!DateTime.TryParse(HeadDomainDic["If-Modified-Since"].FirstOrDefault<string>(), out dateTime))
            {
                return DateTime.FromFileTime(0L);
            }
            return dateTime.ToUniversalTime();
        }

        /// <summary>
        /// 浏览器告诉 WEB 服务器，如果我请求的对象没有改变，就把我缺少的部分给我，如果对象改变了，就把整个对象给我。浏览器通过发送请求对象的 ETag 或者 自己所知道的最后修改时间给 WEB 服务器，让其判断对象是否改变了。总是跟 Range 头部一起使用。  If-Range = "If-Range" ":" ( entity-tag | HTTP-date )
        /// </summary>
        /// <returns></returns>
        public Tuple<int, int> IfRange()
        {
            if (!HeadDomainDic.Keys.Contains("If-Range"))
            {
                return null;
            }
            return ParseIfRange(HeadDomainDic["If-Range"].FirstOrDefault<string>());
        }

        public void SaveToPoll()
        {
            SaveToPoll(this);
        }
        private string Get(string key)
        {
            if (HeadDomainDic == null)
            {
                return string.Empty;
            }
            if (!HeadDomainDic.ContainsKey(key))
            {
                return string.Empty;
            }
            return HeadDomainDic[key].FirstOrDefault<string>();
        }
        private static bool GetCanGzip(string acceptEncodingVlaue)
        {
            if (string.IsNullOrEmpty(acceptEncodingVlaue))
            {
                return false;
            }
            acceptEncodingVlaue = acceptEncodingVlaue.Trim();
            string[] array = acceptEncodingVlaue.Split(new char[]
            {
                ','
            });
            string[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                string text2 = array2[i];
                string text3 = text2.Trim();
                string[] array3 = text3.Split(new char[]
                {
                    ';'
                });
                bool flag = array3[0].ToLower().Trim() == "gzip";
                bool result = array3.Length < 2 || array3[1].Trim() != "q=0";
                if (flag)
                {
                    return result;
                }
            }
            return false;
        }

       

        private static string ParseETag(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            text = text.Trim();
            string text2 = text;
            int num = text2.IndexOf('/');
            if (num != -1)
            {
                text2 = text2.Substring(checked(num + 1));
            }
            text2 = text2.Trim();
            return text2.Trim(new char[]
            {
                '"'
            });
        }

        private static Tuple<int, int> ParseIfRange(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(":", StringComparison.Ordinal) == -1)
            {
                return null;
            }
           
            text = text.Substring(text.IndexOf(':') + 1);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            text = text.Split(new char[]
            {
                ','
            })[0];
            if (text.IndexOf('-') == -1)
            {
                return null;
            }
            int num = text.IndexOf('-');
            if (num == 0)
            {
                int item = 0;
                if (!int.TryParse(text.Substring(1), out item))
                {
                    return null;
                }
                return new Tuple<int, int>(-1, item);
            }
            else if (num == text.Length - 1)
            {
                int num2 = 0;
                if (!int.TryParse(text.Substring(0, text.Length - 1), out num2))
                {
                    return null;
                }
                return new Tuple<int, int>(500, -1);
            }
            else
            {
                string[] array = text.Split(new char[]
                {
                    '-'
                });
                int num3 = 0;
                int num4 = 0;
                if (!int.TryParse(array[0], out num3))
                {
                    return null;
                }
                if (!int.TryParse(array[1], out num4))
                {
                    return null;
                }
                if (num3 > num4)
                {
                    return null;
                }
                return new Tuple<int, int>(num3, num4);
            }
        }
        private static void SaveToPoll(RequestData requestData)
        {
            int num = requestData.GetHashCode();
            if (num < 0)
            {
                num = Math.Abs(num);
            }
            int num2 = num % PollSize;
            lock (_keys)
            {
                if (_reqBuffer[num2] == null)
                {
                    requestData.Host = "";
                    requestData.Port = 0;
                    requestData.RequestQueryString = "";
                    requestData.RequestUrl = "";
                    requestData.HttpMethod = "";
                    requestData.SafeRequestUrl = "";
                    requestData.Socket = null;
                    requestData._preLoadedBody = null;
                    requestData.HeadDomainDic = null;
                    _reqBuffer[num2] = requestData;
                    _keys.Enqueue(num2);
                }
            }
        }


    }

  
}
