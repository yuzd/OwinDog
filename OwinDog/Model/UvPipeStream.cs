using System;

namespace Model
{
   
    public class UvPipeStream : HandleBase
    {
        private static readonly LibUv.PipeConnect_Callback _uv_shutdown_cb = UvShutdownCb;

        private Action<UvPipeStream, int, Exception, object> _callback;

        private object _state;
        public void Init(LoopHandle loopHandle)
        {
            base.Init(loopHandle.LibUv, loopHandle.LibUv.ConnectReqSize, loopHandle.LoopRunThreadId);
        }

      
        public void PipeConnect(UvPipeHandle uvPipeHandle, string text, Action<UvPipeStream, int, Exception, object> callback, object state)
        {
            _callback = callback;
            _state = state;
            Alloc();
            LibUv.PipeConnect(this, uvPipeHandle, text, _uv_shutdown_cb);
        }

        protected override bool ReleaseHandle()
        {
            _Close_Callback(handle);
            handle = IntPtr.Zero;
            return true;
        }
        private static void UvShutdownCb(IntPtr ptrReq, int status)
        {
            UvPipeStream uvPipeStream = GetObjectFromHandel<UvPipeStream>(ptrReq);
            uvPipeStream.DoDispose();
           
            Exception arg = null;
            if (status < 0)
            {
                uvPipeStream.LibUv.GetException(status, out arg);
            }
            try
            {
                uvPipeStream._callback(uvPipeStream, status, arg, uvPipeStream._state);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                uvPipeStream._callback = null;
                uvPipeStream._state = null;
            }
        }

    }
}
