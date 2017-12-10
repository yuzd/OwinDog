using System;
using System.Runtime.InteropServices;

namespace Model
{
    /// <summary>
    /// uv_async is the only thread-safe facility that libuv has.
    /// </summary>
    public class AsyncHandle : HandleBase
    {
        private static readonly LibUv.AsyncInit_callback _uv_async_cb = AsyncCb;

        private Action _callback;

        /// <summary>
        /// uv_asyncがやってくれるのは、async_cbを呼ぶことだけなので、
        /// なにかデータを渡したいときは別途、
        /// pthread_mutexなどを使用して共有データをわたすように自分でよしなにやるかんじですね
        /// </summary>
        public void AsyncSend()
        {
            LibUv.AsyncSend(this);
        }

        public void dispose()
        {
            Dispose();
            ReleaseHandle();
        }

        private  static unsafe void AsyncCb(IntPtr ptrUvAsyncHandle)
        {
            AsyncHandle asyncHandlea = (AsyncHandle)GCHandle.FromIntPtr(*(IntPtr*)((void*)ptrUvAsyncHandle)).Target;
            if (asyncHandlea == null)
            {
                return;
            }
            try
            {
                asyncHandlea._callback();
            }
            catch
            {
                //ignore
            }
        }

        /// <summary>
        /// 为loop注册了一个异步消息监听器 其他线程就可以通过async监视器给主线程发送消息
        /// </summary> 
        /// <param name="loopHandle"></param>
        /// <param name="cb"></param>
        public void Init(LoopHandle loopHandle, Action cb)
        {
            base.Init(loopHandle.LibUv, loopHandle.LibUv.HandSize(LibUv.HandleType.UV_ASYNC), loopHandle.LoopRunThreadId);
            _callback = cb;
            LibUv.AsyncInit(loopHandle, this, _uv_async_cb);
        }


    }
}
