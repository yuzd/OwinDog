using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Service;
using Util;

namespace OwinEngine
{
    public sealed class OwinHttpWorker
    {
        private byte[] _requestData;

        private int _requestDataOffset;

        private int _requestDataSize;

        private static readonly SimpleThreadPool _simpleThreadPool;
        static OwinHttpWorker()
        {
            int threadSize = Environment.ProcessorCount + 1;
            if (threadSize < 2)
            {
                threadSize = 2;
            }
            if (threadSize > 64)
            {
                threadSize = 64;
            }
            _simpleThreadPool = new SimpleThreadPool(threadSize);
        }

        public OwinHttpWorker()
            : this(null)
        {
        }

        public OwinHttpWorker(byte[] array)
        {
            _requestData = RequestDataFactory.Create();

            if (array != null && array.Length > 0)
            {
                Buffer.BlockCopy(array, 0, _requestData, 0, array.Length);
                _requestDataOffset += array.Length;
                _requestDataSize += array.Length;
            }
        }

        /// <summary>
        /// 执行请求的入口
        /// </summary>
        /// <param name="owinSocket"></param>
        public void Start(OwinSocket owinSocket)
        {
            if (_requestDataSize > 13)
            {
                int handleSize = CommonUtil.GetBytesRealLength(_requestData, _requestDataSize, false);
                if (handleSize > 0)
                {
                    AddToProcessThread(handleSize, owinSocket);
                    return;
                }
            }
            if (_requestDataSize > 4000)
            {
                owinSocket.Dispose();
                RequestDataFactory.Recover(_requestData);
                _requestData = null;
                return;
            }
            owinSocket.Read(new Action<OwinSocket, byte[], int, Exception, object>(OnReadCompleteCallback), null);
        }

        /// <summary>
        /// 解析报文 到 owin 协议
        /// </summary>
        /// <param name="state"></param>
        private void ProcessRequestData(object state)
        {
            WorkThreadState workThreadState = (WorkThreadState)state;
            int handleSize = workThreadState.HandleSize;
            int offset = 0;
            //截取 http方法 请求路径 http版本 
            byte[] protocol = CommonUtil.GetProtocolBytes(_requestData, 0, handleSize, ref offset);
            if (protocol == null || offset == 0 || CommonUtil.CheckBadRequest(ref protocol) == 0)
            {
                //不存在 http 请求行
                byte[] array2 = ResultFactory.FormartBR400(string.Format("Bad Request: '{0}'", (protocol == null) ? "null" : Encoding.ASCII.GetString(protocol)));
                workThreadState.OwinSocket.Write(array2, new Action<OwinSocket, int, Exception, object>(OnWriteCompleteToClose), null);
                RequestDataFactory.Recover(_requestData);
                _requestData = null;
                return;
            }
            //消息报头
            byte[] headDomainBytes = new byte[handleSize - offset];
            Buffer.BlockCopy(_requestData, offset, headDomainBytes, 0, headDomainBytes.Length);

            byte[] preLoadedBodyBytes = null;
            if (handleSize < _requestDataSize)
            {
                //如果传输
                preLoadedBodyBytes = new byte[_requestDataSize - handleSize];
                Buffer.BlockCopy(_requestData, handleSize, preLoadedBodyBytes, 0, preLoadedBodyBytes.Length);
            }

            IDictionary<string, string[]> dictionary = CommonUtil.ConvertByteToDic(headDomainBytes);

            RequestData requestData = RequestData.New();
            requestData._preLoadedBody = preLoadedBodyBytes;
            requestData.HeadDomainDic = dictionary;

            string protocolStr = Encoding.ASCII.GetString(protocol);
            int index = protocolStr.IndexOf(' ');
            requestData.HttpMethod = protocolStr.Substring(0, index).ToUpper();
            protocolStr = protocolStr.Substring(index + 1);
            index = protocolStr.LastIndexOf(' ');
            requestData.RequestUrl = protocolStr.Substring(0, index).Trim();
            requestData.HttpVersion = protocolStr.Substring(index).Trim();
            index = requestData.RequestUrl.IndexOf('?');
            if (index == 0)
            {
                requestData.RequestQueryString = requestData.RequestUrl;
                requestData.RequestUrl = "/";
            }
            else if (index > 0)
            {
                string text2 = requestData.RequestUrl.Substring(0, index).Trim();
                string b = requestData.RequestUrl.Substring(index + 1);
                requestData.RequestUrl = text2;
                requestData.RequestQueryString = b;
                if (!string.IsNullOrEmpty(requestData.RequestQueryString))
                {
                    requestData.RequestQueryString = requestData.RequestQueryString.Trim();
                }
            }
            requestData.SafeRequestUrl = GetSafePath(requestData.RequestUrl);
            string host = (requestData.HeadDomainDic == null || !requestData.HeadDomainDic.ContainsKey("Host")) ? string.Empty : requestData.HeadDomainDic["Host"].FirstOrDefault<string>();
            if (string.IsNullOrEmpty(host))
            {
                host = ((requestData.HeadDomainDic == null || !requestData.HeadDomainDic.ContainsKey("host")) ? string.Empty : requestData.HeadDomainDic["host"].FirstOrDefault<string>());
            }
            if (!string.IsNullOrEmpty(host))
            {
                ParseHost(host, ref requestData.Host, ref requestData.Port);
            }
            else
            {
                requestData.Host = "localhost";
                requestData.Port = 80;
            }
            requestData.Socket = workThreadState.OwinSocket;
            RequestDataFactory.Recover(_requestData);
            _requestData = null;
            _requestDataOffset = 0;
            _requestDataSize = 0;

            OwinTask.ExcuteWorkTask(requestData);
        }

        /// <summary>
        /// 过滤不安全的字符
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string GetSafePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }
            bool flag = path.EndsWith("/");
            path = path.Replace("\\", "/");
            while (path.IndexOf('\\') != -1)
            {
                path = path.Replace('\\', '/');
            }
            string[] array = path.Split(new char[]
            {
                '/'
            });
            string[] array2 = new string[array.Length];
            int num = 0;
            int num2 = array.Length;
            for (int i = 0; i < num2; i++)
            {
                string text2 = array[i];
                if (!string.IsNullOrEmpty(text2) && !(text2 == "."))
                {
                    if (text2 == ".." || text2 == "...")
                    {
                        if (num > 0)
                        {
                            num--;
                        }
                    }
                    else
                    {
                        if (text2.IndexOf("....") != -1)
                        {
                            return "/";
                        }
                        array2[num] = text2;
                        num++;
                    }
                }
            }
            if (num == 0)
            {
                return "/";
            }
            string[] array3;
            if (flag)
            {
                array3 = new string[num + 2];
            }
            else
            {
                array3 = new string[num + 1];
            }
            Array.Copy(array2, 0, array3, 1, num);
            return string.Join("/", array3);
        }

        /// <summary>
        /// 放入线程池
        /// </summary>
        /// <param name="handleSize"></param>
        /// <param name="owinSocket"></param>
        private void AddToProcessThread(int handleSize, OwinSocket owinSocket)
        {
            _simpleThreadPool.UnsafeQueueUserWorkItem(new Action<object>(ProcessRequestData), new WorkThreadState
            {
                HandleSize = handleSize,
                OwinSocket = owinSocket
            });
        }

        /// <summary>
        /// 解析domain和端口
        /// </summary>
        /// <param name="hostLine"></param>
        /// <param name="hostDomain"></param>
        /// <param name="hostPort"></param>
        private static void ParseHost(string hostLine, ref string hostDomain, ref int hostPort)
        {
            if (!string.IsNullOrEmpty(hostLine))
            {
                hostLine = hostLine.Trim();
            }
            if (string.IsNullOrEmpty(hostLine))
            {
                return;
            }
            if (hostLine.IndexOf(':') == -1)
            {
                hostDomain = hostLine;
                hostPort = 0;
                return;
            }
            string[] array = hostLine.Split(new char[]
            {
                ':'
            });
            hostDomain = array[0].Trim();
            try
            {
                int num = 0;
                if (array[1].Trim() != "" && int.TryParse(array[1].Trim(), out num))
                {
                    hostPort = num;
                }
            }
            catch
            {
                //ignore
            }
        }

        /// <summary>
        /// 写操作完毕的回调事件
        /// </summary>
        /// <param name="owinSocket"></param>
        /// <param name="status"></param>
        /// <param name="ex"></param>
        /// <param name="state"></param>
        private void OnWriteCompleteToClose(OwinSocket owinSocket, int status, Exception ex, object state)
        {
            owinSocket.Dispose();
        }

        /// <summary>
        /// 读操作
        /// </summary>
        /// <param name="owinSocket"></param>
        /// <param name="buffer"></param>
        /// <param name="nread"></param>
        /// <param name="ex"></param>
        /// <param name="state"></param>
        private void OnReadCompleteCallback(OwinSocket owinSocket, byte[] buffer, int nread, Exception ex, object state)
        {
            if (nread < 1 || ex != null || buffer == null)
            {
                owinSocket.Dispose();
                RequestDataFactory.Recover(_requestData);
                _requestData = null;
                return;
            }
            Buffer.BlockCopy(buffer, 0, _requestData, _requestDataOffset, nread);

            _requestDataOffset += nread;
            _requestDataSize += nread;
            if (_requestDataSize > 15)
            {
                int length = CommonUtil.GetBytesRealLength(_requestData, _requestDataSize, false);
                if (length > 0)
                {
                    AddToProcessThread(length, owinSocket);
                    return;
                }
            }
            if (_requestDataSize > 4000)
            {
                owinSocket.Dispose();
                RequestDataFactory.Recover(_requestData);
                _requestData = null;
                return;
            }
            owinSocket.Read(new Action<OwinSocket, byte[], int, Exception, object>(OnReadCompleteCallback), null);
        }



        private class WorkThreadState
        {
            public int HandleSize;

            public OwinSocket OwinSocket;
        }
    }
}
