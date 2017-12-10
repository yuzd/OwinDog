using System;
using System.Threading;

namespace Model
{
  
    public class LoopHandle : HandleBase
    {
        public void Stop()
        {
            LibUv.Stop(this);
        }

        /// <summary>
        /// 把监视器和loop联系起来
        /// </summary>
        /// <param name="libUv"></param>
        public void Init(LibUv libUv)
        {
            base.Init(libUv, libUv.GetUvLoopSize(), Thread.CurrentThread.ManagedThreadId);
            LibUv.Init(this);//loop init
        }


        public int Start(int num = 0/*UV_RUN_DEFAULT*/)
        {
            return LibUv.Run(this, num);
        }

        protected override unsafe bool ReleaseHandle()
        {
            IntPtr ptr = handle;
            if (ptr != IntPtr.Zero)
            {
                IntPtr intPtr = *(IntPtr*)((void*)ptr);
                try
                {
                    LibUv.LoopClose(this);
                }
                catch
                {
                    //ignore
                }
                handle = IntPtr.Zero;
                FreeHandle(ptr, intPtr);
            }
            return true;
        }

    }
}
