using System;
using System.Net;

namespace Model
{
  
    /// <summary>
    /// lib uv 的 tcp handle
    /// </summary>
    public class ListenHandle : UvStreamHandle
    {
        public LoopHandle Loop { get; set; }

     

        public void TcpBind(IPEndPoint iPEndPoint)
        {
            string text = iPEndPoint.Address.ToString();
            LibUv.Addr addr;
            Exception ex;
            LibUv.Ip4Address(text, iPEndPoint.Port, out addr, out ex);
            if (ex != null)
            {
                Exception ex2;
                LibUv.Ip6Address(text, iPEndPoint.Port, out addr, out ex2);
                if (ex2 != null)
                {
                    throw ex;
                }
            }
            LibUv.TcpBind(this, ref addr, 0);
        }

        public void TcpNodealy(bool flag)
        {
            LibUv.TcpNodealy(this, flag);
        }

        public void Init(LoopHandle loopHandle, Action<Action<object>, object> asyncSendUserPostAction)
        {
            base.Init(loopHandle.LibUv, loopHandle.LibUv.TcpHandleSize, loopHandle.LoopRunThreadId);
            LibUv.TcpInit(loopHandle, this);
            Loop=loopHandle;
            _postAsync = asyncSendUserPostAction;
        }

        public void TcpBind(string ipString, int port)
        {
            IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Parse(ipString), port);
            TcpBind(iPEndPoint);
        }

    }
}
