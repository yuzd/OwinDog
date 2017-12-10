using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Service
{

    
    
    public sealed class CustomeAsyncResult : IAsyncResult
    {
        public CustomeAsyncResult(object obj)
        {
            AsyncState = obj;
            AsyncWaitHandle = new AutoResetEvent(false);
        }


        public byte[] RecvBuffer { get; set; }


        public int RecvLength { get; set; }



        public int RecvOffset { get; set; }

        public object AsyncState { get; set; }

        public AsyncCallback UserCallbackFunc { get; set; }


        public WaitHandle AsyncWaitHandle { get; set; }

        public int RealRecvSize { get; set; }


        public bool IsCompleted { get; set; }


        internal bool SocketIsErrOrClose { get; set; }

       

        internal bool SocketIsTimeOut{ get; set; }

        public bool CompletedSynchronously {
            get { return false; }
        }




    }


}
