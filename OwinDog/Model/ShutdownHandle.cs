using System;

namespace Model
{
    public class ShutdownHandle : HandleBase
    {

        private static readonly LibUv.ShutDown_Callback _ShutDown_Callback = new LibUv.ShutDown_Callback(ShutDown_Callback);

        private Action<int, object> _callBack;

        private object _state;

        public void Init(LoopHandle loopHandle)
        {
            base.Init(loopHandle.LibUv, loopHandle.LibUv.ShutdownReqSize, loopHandle.LoopRunThreadId);
        }

        public void ShutDown(UvStreamHandle uvStreamHandle, Action<int, object> callBack, object state)
        {

            _callBack = callBack;
            _state = state;
            Alloc();
            try
            {
                LibUv.ShutDown(this, uvStreamHandle, _ShutDown_Callback);
            }
            catch (Exception ex)
            {
                DoDispose();
                throw ex;
            }
        }

        private static void ShutDown_Callback(IntPtr intPtr, int arg)
        {
            ShutdownHandle shutdownHandle = GetObjectFromHandel<ShutdownHandle>(intPtr);
            if (shutdownHandle == null || shutdownHandle._callBack == null)
            {
                return;
            }
            try
            {
                shutdownHandle._callBack(arg, shutdownHandle._state);
            }
            catch
            {
                //ignore
            }
            shutdownHandle.DoDispose();
            shutdownHandle.Dispose();
            shutdownHandle._callBack = null;
            shutdownHandle._state = null;
        }

        protected override bool ReleaseHandle()
        {
            _Close_Callback(handle);
            handle = IntPtr.Zero;
            return true;
        }

    }
}
