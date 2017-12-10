using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Model
{
    public abstract class HandleBase : SafeHandle
    {
        protected static LibUv.Close_Callback _Close_Callback = new LibUv.Close_Callback(Free);

        protected Action<Action<object>, object> _postAsync;

        //帮我们在地址和对象之间进行转换
        private GCHandle _GCHandle;


        protected HandleBase() : base(IntPtr.Zero, true)
        {
        }

        public LibUv LibUv { get; protected set; }

        /// <summary>
        /// 跑 loopRun的 线程Id
        /// </summary>
        public int LoopRunThreadId { get; set; }




        #region 释放
        protected static unsafe void Free(IntPtr intPtr)
        {
            if (intPtr == IntPtr.Zero)
            {
                return;
            }
            FreeHandle(intPtr, *(IntPtr*)((void*)intPtr));
        }
        protected static void FreeHandle(IntPtr intPtr, IntPtr intPtr2)
        {
            if (intPtr2 != IntPtr.Zero)
            {
                try
                {
                    //返回从某个托管对象的句柄创建的新 GCHandle 对象
                    GCHandle.FromIntPtr(intPtr2).Free();
                }
                catch
                {
                    //ignore
                }
            }
            if (intPtr == IntPtr.Zero)
            {
                return;
            }
            //释放由非托管 COM 任务内存分配器使用 Marshal.AllocCoTaskMem 分配的内存块
            Marshal.FreeCoTaskMem(intPtr);
        }
        #endregion
        public void Debug(bool flag = false)
        {
            if (!flag && IsClosed)
            {
                Console.WriteLine("DEBUG: OwinDog.UvHandle.Validate: Handle is closed.");
            }
            if (IsInvalid)
            {
                Console.WriteLine("DEBUG: OwinDog.UvHandle.Validate: Handle is invalid.");
            }
        }

        public static unsafe T GetObjectFromHandel<T>(IntPtr value)
        {
            return (T)((object)GCHandle.FromIntPtr(*(IntPtr*)((void*)value)).Target);
        }



        protected unsafe void Init(LibUv libuv, int hdle, int point)
        {
            LibUv = libuv;
            LoopRunThreadId = point;
            //Starting with libuv v1.0, users should allocate the memory for the loops before initializing it with uv_loop_init(uv_loop_t *). This allows you to plug in custom memory management
            handle = Marshal.AllocCoTaskMem(hdle);//申请内存
            *(IntPtr*)((void*)handle) = GCHandle.ToIntPtr(GCHandle.Alloc(this, GCHandleType.Weak));
        }

        public IntPtr InternalGetHandle()
        {
            return handle;
        }

        public virtual void Alloc()
        {
            _GCHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        }

        public virtual void DoDispose()
        {
            _GCHandle.Free();
        }

        public void UvRef()
        {
            LibUv.UvRef(this);
        }

        public void UvUnRef()
        {
            LibUv.UvUnRef(this);
        }

        public override bool IsInvalid
        {
            get
            {
                return handle == IntPtr.Zero;
            }
        }

        protected override bool ReleaseHandle()
        {
            IntPtr intPtr = Interlocked.Exchange(ref handle, IntPtr.Zero);
            if (intPtr != IntPtr.Zero)
            {
                if (Thread.CurrentThread.ManagedThreadId != LoopRunThreadId)
                {
                    if (_postAsync != null)
                    {
                        HandleRelease handleRelease = new HandleRelease();
                        handleRelease.LibUv = LibUv;
                        _postAsync(new Action<object>(handleRelease.Release), intPtr);
                    }
                }
                else
                {
                    LibUv.UvClose(intPtr, _Close_Callback);
                }
            }
            return true;
        }



        private sealed class HandleRelease
        {
            public void Release(object obj)
            {
                LibUv.UvClose((IntPtr)obj, _Close_Callback);
            }

            public LibUv LibUv;
        }
    }
}
