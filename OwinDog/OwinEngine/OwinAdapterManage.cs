using System;
using Service;

namespace OwinEngine
{

    public sealed class OwinAdapterManage
    {
        private static readonly OwinManager _owinManager = new OwinManager(OnOwinCallCompleteCallback);

        public bool Process(RequestData requestData)
        {
            return _owinManager != null && _owinManager.Process(requestData);
        }

        /// <summary>
        /// 如果Connection Close掉了 关闭tcp 否则保持Tcp socket长连接
        /// </summary>
        /// <param name="req"></param>
        /// <param name="iskeep"></param>
        private static void OnOwinCallCompleteCallback(RequestData req, bool iskeep)
        {
            if (!iskeep || !req.IsKeepAlive())//Connection 是否Close掉了
            {
                req.Socket.Dispose();
                req.SaveToPoll();
                return;
            }
            OwinHttpWorkerManage.Start((OwinSocket)req.Socket, req._preLoadedBody);
            req.SaveToPoll();
        }

 
    }
}
