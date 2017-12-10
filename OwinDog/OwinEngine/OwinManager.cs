using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Service;

namespace OwinEngine
{
    public class OwinManager
    {
        private readonly OwinAdapter _owinAdapter = ApplicationInfo.OwinAdapter;

        private readonly Func<IDictionary<string, object>, Task> _owinMainFunc;

        public OwinCallCompleteCallback _owinCallCompleteCallback;

        private readonly IDictionary<string, object> Capabilities = new Dictionary<string, object>();

        public delegate void OwinCallCompleteCallback(RequestData requestData, bool flag = false);
        public OwinManager(OwinCallCompleteCallback owinCallCompleteCallback)
        {
            _owinMainFunc = _owinAdapter.OwinMain;
            _owinCallCompleteCallback = owinCallCompleteCallback;
            Capabilities.Add("websocket.Version", "1.0");
            Capabilities.Add("server.Name", "OwinDog");
            Capabilities.Add("host.AppName", "OwinDog");
        }

        public bool Process(RequestData reqData)
        {
            OwinWapper owinWapper = new OwinWapper();
            owinWapper.ReqData = reqData;
            owinWapper.OwinManager = this;
            if (_owinMainFunc == null)
            {
                return false;
            }
            ISocket socket = owinWapper.ReqData.Socket;
            //是否是基于 http/1.1协议的
            owinWapper.Keep = owinWapper.ReqData.IsKeepAlive();
            owinWapper.OwinContext = new OwinContext("1.0", CancellationToken.None, owinWapper.ReqData.HeadDomainDic);
            owinWapper.OwinContext.Set("server.RemoteIpAddress", socket.GetRemoteIpAddress());
            owinWapper.OwinContext.Set("server.RemotePort", socket.GetRemoteIpPort().ToString());
            owinWapper.OwinContext.Set("server.LocalIpAddress", socket.LocalIpAddress());
            owinWapper.OwinContext.Set("server.LocalPort", socket.LocalIpPort().ToString());
            owinWapper.OwinContext.Set("server.IsLocal", socket.LocalIpAddress() == "127.0.0.1" || socket.GetRemoteIpAddress() == socket.LocalIpAddress());
            owinWapper.OwinContext.Set("owin.CallCancelled", CancellationToken.None);
            owinWapper.OwinContext.Set("ssl.ClientCertificate", null);
            owinWapper.OwinContext.Set("server.Capabilities", Capabilities);
            owinWapper.OwinContext.Set("owin.RequestProtocol", owinWapper.ReqData.HttpVersion);
            owinWapper.OwinContext.Set("owin.RequestMethod", owinWapper.ReqData.HttpMethod);
            owinWapper.OwinContext.Set("owin.RequestQueryString", owinWapper.ReqData.RequestQueryString);
            owinWapper.OwinContext.Set("owin.RequestScheme", "http");
            owinWapper.OwinContext.Set("owin.RequestPathBase", string.Empty);
            owinWapper.OwinContext.Set("owin.RequestPath", owinWapper.ReqData.SafeRequestUrl);
            OwinStream owinReqStream = new OwinStream(owinWapper.ReqData);
            OwinStream owinRepStream = new OwinStream(owinWapper.ReqData);
            int num = 0;
            string text = owinWapper.ReqData.HeadDomainDic.Keys.Contains("Content-Length") ? owinWapper.ReqData.HeadDomainDic["Content-Length"].FirstOrDefault<string>() : string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                if (!int.TryParse(text, out num))
                {
                    num = 0;
                }
                num = ((num < 1) ? ((owinWapper.ReqData._preLoadedBody == null) ? 0 : owinWapper.ReqData._preLoadedBody.Length) : num);
            }
            OwinRequestStream owinRequestStream = new OwinRequestStream(owinReqStream, num);
            OwinResponseStream owinsResponseStream = new OwinResponseStream(owinWapper.OwinContext, owinRepStream, owinWapper.Keep);
            owinWapper.OwinContext.Set("server.OnSendingHeaders", new Action<Action<object>, object>(owinsResponseStream.AddToCallbacksList));
            owinWapper.OwinContext.RequestBody = owinRequestStream;
            owinWapper.OwinContext.ResponseBody = owinsResponseStream;
            bool isWebSocket = false;
            if (owinWapper.ReqData.HeadDomainDic.Keys.Contains("Connection"))
            {
                //通过请求头中的Connection是不是等于Upgrade以及Upgrade是否等于WebSocket
                string connection = owinWapper.ReqData.HeadDomainDic["Connection"].FirstOrDefault<string>();
                if (connection != null && (connection.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) > -1 && owinWapper.ReqData.HeadDomainDic.Keys.Contains("Upgrade")))
                {
                    string upgrade = owinWapper.ReqData.HeadDomainDic["Upgrade"].FirstOrDefault<string>();
                    if (string.Equals(upgrade, "WebSocket", StringComparison.OrdinalIgnoreCase))
                    {
                        isWebSocket = true;
                    }
                }
            }
            OwinWebSocketServer owinWebSocketServer = null;
            if (isWebSocket)
            {
                var action = new Action(owinWapper.CallCompleteCallbackToCloseTcp);
                owinWebSocketServer = new OwinWebSocketServer(owinReqStream, owinRepStream, owinWapper.ReqData.HeadDomainDic, action);
                owinWapper.OwinContext.Set("websocket.Version", "1.0");
                owinWapper.OwinContext.Set("websocket.Accept", owinWebSocketServer.Accept);
            }
            Task task = _owinMainFunc(owinWapper.OwinContext);
            if (task == null)
            {
                return false;
            }
            if (owinWebSocketServer != null && owinWebSocketServer.IsCompleted())
            {
                return true;
            }
            if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
            {
                WorkTaskComplete(task, owinWapper.ReqData, owinWapper.OwinContext, owinWapper.Keep);
            }
            else
            {
                task.ContinueWith(new Action<Task>(owinWapper.WorkTaskComplete));
            }
            return true;
        }

     

        private void WorkTaskComplete(Task task, RequestData requestData, OwinContext owinContext, bool keep)
        {
            if (task.IsCompleted)
            {
                ResponseEnd(requestData, owinContext, keep);
                return;
            }

            if (owinContext != null && owinContext.ResponseBody != null)
            {
                try
                {
                    ((OwinResponseStream)owinContext.ResponseBody).Dispose();
                }
                catch
                {
                    //ignore
                }
            }
            _owinCallCompleteCallback(requestData, false);//关闭Tcp
        }

        /// <summary>
        /// 读取完毕 判断是否Connection 保持着 如果是不关闭tcp链接
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="owinContext"></param>
        /// <param name="iskeep"></param>
        private void ResponseEnd(RequestData requestData, OwinContext owinContext, bool iskeep)
        {
            bool flag = false;
            if (owinContext != null && owinContext.ResponseBody != null)
            {
                try
                {
                    OwinResponseStream owinResponseStream = (OwinResponseStream)owinContext.ResponseBody;
                    owinResponseStream.ResponseEnd();
                    flag = !owinResponseStream.ConnectionClosed;
                    owinResponseStream.Dispose();
                }
                catch
                {
                    //ignore
                }
            }
            bool flag3 = iskeep && flag;
            _owinCallCompleteCallback(requestData, flag3);
        }

        private sealed class OwinWapper
        {
            public bool Keep;

            public OwinContext OwinContext;

            public OwinManager OwinManager;

            public RequestData ReqData;
            public void CallCompleteCallbackToCloseTcp()
            {
                OwinManager._owinCallCompleteCallback(ReqData, false);//关闭Tcp
            }

            public void WorkTaskComplete(Task task)
            {
                OwinManager.WorkTaskComplete(task, ReqData, OwinContext, Keep);
            }

        }
    }
   
}
