using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Service;
using Util;

namespace OwinEngine
{
    public static class OwinTask
    {

        private static readonly Dictionary<string, bool> _dynamicReqDic;

        private static readonly byte[] Continue100;


        static OwinTask()
        {
            _dynamicReqDic = new Dictionary<string, bool>();
            Continue100 = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\nServer: Owin\r\nX-Owin-Server: OwinDog\r\nContent-Length: 0\r\n\r\n");
            _dynamicReqDic["aspx"] = true;
            _dynamicReqDic["ashx"] = true;
            _dynamicReqDic["ascx"] = true;
            _dynamicReqDic["asmx"] = true;
            _dynamicReqDic["svc"] = true;
            _dynamicReqDic["cshtml"] = true;
            _dynamicReqDic["cshtm"] = true;
            _dynamicReqDic["sshtm"] = true;
            _dynamicReqDic["sshtml"] = true;
            _dynamicReqDic["vbhtml"] = true;
            _dynamicReqDic["php"] = true;
            _dynamicReqDic["jsp"] = true;
            _dynamicReqDic["do"] = true;
        }

        /// <summary>
        /// 用Owin 适配器去执行请求
        /// </summary>
        /// <param name="requestData"></param>
        public static void ExcuteWorkTask(RequestData requestData)
        {
            try
            {
                AddWorkTask(requestData);
            }
            catch (Exception ex)
            {
                requestData.Socket.Dispose();
                if (!(ex is SocketException))
                {
                    Console.WriteLine();
                    Console.WriteLine("OwinDog DEBUG: ============================================================");
                    Console.WriteLine(ex);
                }
            }
        }

        private static void AddWorkTask(RequestData requestData)
        {
            if (requestData.SafeRequestUrl.Length > 3 && !RequestCheck.IsNotSafeRequest(requestData.SafeRequestUrl))
            {
                HttpWorker.EndRequest(requestData.Socket, 400, "Path:" + requestData.SafeRequestUrl);
                requestData.SaveToPoll();
                return;
            }
            string fileExt = CommonUtil.GetFileExtention(requestData.SafeRequestUrl);
            if (!string.IsNullOrEmpty(fileExt))
            {
                fileExt = fileExt.ToLower();
            }

            #region 静态文件请求处理

            if (!string.IsNullOrEmpty(fileExt) && !_dynamicReqDic.ContainsKey(fileExt) && requestData.HttpMethod == "GET")
            {
                HttpWorker httpWorker = new HttpWorker();
                if (httpWorker.Process(requestData))
                {
                    return;
                }
            }
            #endregion

            #region 动态请求处理
            if (ApplicationInfo.OwinAdapter != null)
            {
                if ((requestData.HttpMethod == "POST" || requestData.HttpMethod == "PUT") 
                    && requestData.HeadDomainDic != null && requestData.HeadDomainDic.Keys.Contains("Expect"))
                {
                    //http 100-continue用于客户端在发送POST数据给服务器前，征询服务器情况，看服务器是否处理POST的数据，如果不处理，客户端则不上传POST数据，如果处理，则POST上传数据。在现实应用中，通过在POST大数据时，才会使用100-continue协议。 Client发送Expect:100-continue消息
                    //在使用curl做POST的时候, 当要POST的数据大于1024字节的时候, curl并不会直接就发起POST请求, 而是会分为俩步,
                    //1. 发送一个请求, 包含一个Expect:100-continue, 询问Server使用愿意接受数据
                    //2. 接收到Server返回的100-continue应答以后, 才把数据POST给Server
                    //大致功能好像是当post数据超过1024时，不用询问服务器是否接受其他数据
                    string text2 = requestData.HeadDomainDic["Expect"].FirstOrDefault<string>();
                    if (string.Equals(text2, "100-continue", StringComparison.OrdinalIgnoreCase))
                    {
                        requestData.Socket.Write(Continue100, WriteCallBack, null);
                        requestData.HeadDomainDic.Remove("Expect");
                    }
                }
                OwinAdapterManage owinAdapterManage = new OwinAdapterManage();
                if (owinAdapterManage.Process(requestData))
                {
                    return;
                }
            }

            #endregion
            #region 请求Info信息

            if (requestData.SafeRequestUrl == "/info")
            {
                SendWelcomePage((OwinSocket)requestData.Socket, requestData);
                return;
            } 
            #endregion

            HttpWorker.EndRequest(requestData.Socket, 404, requestData.RequestUrl);
            requestData.SaveToPoll();
        }

       

        private static void SendWelcomePage(OwinSocket socket, RequestData requestData)
        {
            string text = "<!DOCTYPE html>\r\n<html><head><title>OwinDog Server</title></head><body><h2 style=\"color:red;\">Hello, OwinDog Host Server!</h2><br/><span style=\"font-size:14px;\">" + DateTime.Now.ToString("yyy-MM-dd HH:mm:ss") + "</span></body></html>";
            string s = string.Format("HTTP/1.1 200 OK\r\nServer: OwinDog/1.0\r\nContent-Length:{0}\r\n\r\n{1}", Encoding.ASCII.GetByteCount(text), text);
            socket.Write(Encoding.ASCII.GetBytes(s), new Action<OwinSocket, int, Exception, object>(WriteCallBack), requestData);
        }

    

        private static void WriteCallBack(OwinSocket socket, int num, Exception ex, object obj)
        {
            RequestData requestData = obj as RequestData;
            if (requestData == null)
            {
                socket.Dispose();
                return;
            }
            if (!requestData.IsKeepAlive() || ex != null)
            {
                requestData.SaveToPoll();
                socket.Dispose();
                return;
            }
            byte[] preLoadedBody = requestData._preLoadedBody;
            requestData.SaveToPoll();
            OwinHttpWorkerManage.Start(socket, preLoadedBody);
        }

       
    

 
    }

}
