using System;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Util;

#pragma warning disable 649

namespace Model
{
    public class LibUv
    {
        public int TcpHandleSize { get; set; }
        public int WriteReqSize { get; set; }

        public int ShutdownReqSize { get; set; }
        public int NamePipeHandleSize { get; set; }
        public int ConnectReqSize { get; set; }


        public LibUv()
        {
            IsWindows = SystemUtil.IsWindowOs();
            if (!IsWindows)
            {
                IsDarwin = SystemUtil.IsDarwin();
            }
        }

        /// <summary>
        /// 初始化与libuv的函数库
        /// </summary>
        /// <param name="libuvPath"></param>
        public void Initialization(string libuvPath)
        {
            //初始化 LoadLibrary FreeLibrary  GetProcAddress api函数
            SystemUtil.Init(this);
            //加载libuv dll
            IntPtr intPtr = LoadLibrary(libuvPath);
            if (intPtr == IntPtr.Zero)
            {
                string text = "Unable to load libuv.";
                if (!IsWindows && !IsDarwin)
                {
                    text += " Make sure libuv is installed and available as libuv.so.1";
                }
                throw new InvalidOperationException(text);
            }
            //遍历libuv的变量 从libuv的dll文件里面 获取方法对应的委托
            foreach (FieldInfo current in GetType().GetTypeInfo().DeclaredFields)
            {
                IntPtr intPtr2 = GetProcAddress(intPtr, current.Name.TrimStart(new char[]
                {
                    '_'
                }));
                if (!(intPtr2 == IntPtr.Zero))
                {
                    Delegate delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer(intPtr2, current.FieldType);
                    current.SetValue(this, delegateForFunctionPointer);
                }
            }
            TcpHandleSize = HandSize(HandleType.UV_TCP);
            NamePipeHandleSize = HandSize(HandleType.UV_NAMED_PIPE);
            WriteReqSize = ReqestSize(RequestType.UV_WRITE);
            ShutdownReqSize = ReqestSize(RequestType.UV_SHUTDOWN);
            ConnectReqSize = ReqestSize(RequestType.UV_CONNECT);
        }

        public void Init(LoopHandle loopHandle)
        {
            CheckError(_uv_loop_init(loopHandle));
        }

        public void LoopClose(LoopHandle loopHandle)
        {
            CheckError(_uv_loop_close(loopHandle.InternalGetHandle()));
        }

        /// <summary>
        /// Async handles are active when you do uv_async_init, so unless you uv_unref them, the loop will stay alive.
        /// </summary>
        /// <param name="handleBase"></param>
        public void UvRef(HandleBase handleBase)
        {
            _uv_ref(handleBase);
        }

        /// <summary>
        /// The libuv event loop (if run in the default mode) will run until there are no active and referenced handles left.
        ///call this method for unref handle
        /// </summary>
        /// <param name="handleBase"></param>
        public void UvUnRef(HandleBase handleBase)
        {
            _uv_unref(handleBase);
        }

        /// <summary>
        /// uv_async_send（线程通信），消息的发送是异步的，在另外一个线程中多次（二次或更多）调用了uv_async_send函数后它只会保证uv_async_init回调函数至少被调用一次
        ///uv_async_send是非阻塞的，同样也不是线程安全的，在变量访问时应该尽量和互斥量或读写锁来保证访问顺序
        /// </summary>
        /// <param name="asyncHandle"></param>
        public void AsyncSend(AsyncHandle asyncHandle)
        {
            CheckError(_uv_async_send(asyncHandle));
        }

        public int PipePendingCount(UvPipeHandle uvPipeHandle)
        {
            return _uv_pipe_pending_count(uvPipeHandle);
        }

        public void ReadStop(UvStreamHandle uvStreamHandle)
        {
            CheckError(_uv_read_stop(uvStreamHandle));
        }

        public int HandSize(HandleType handleType)
        {
            return _uv_handle_size(handleType);
        }

        public int ReqestSize(RequestType requestType)
        {
            return _uv_req_size(requestType);
        }

        public unsafe IPEndPoint GetIpEndPoint(UvStreamHandle uvStreamHandle)
        {
            Addr addr = default(Addr);
            int num = Marshal.SizeOf(typeof(Addr));
            CheckError(_uv_tcp_getsockname(uvStreamHandle, out addr, out num));
            byte* ptr = (byte*)(&addr);
            ushort port;
            uint num2;
            checked
            {
                port = (ushort)(((int)ptr[2] << 8) + (int)ptr[3]);
                num2 = (uint)(*(ushort*)(ptr + 6));
                num2 = (num2 << 16) + (uint)(*(ushort*)(ptr + 4));
            }
            return new IPEndPoint((long)((ulong)num2), (int)port);
        }

        /// <summary>
        /// 获取Libuv的报错信息
        /// </summary>
        /// <param name="returnValue">libuv函数的返回值 -1代表出错 0代表成功</param>
        /// <param name="err">Exception</param>
        /// <returns></returns>
        public int GetException(int returnValue, out Exception err)
        {
            if (returnValue < 0)
            {
                string ErrDescription = GetErrDescription(returnValue);
                string StrError = GetStrError(returnValue);
                err = new Exception(string.Concat(new object[]
                {
                    "Error ",
                    returnValue,
                    " ",
                    ErrDescription,
                    " ",
                    StrError
                }));
            }
            else
            {
                err = null;
            }
            return returnValue;
        }
        public BufferStruct CreateBufferStruct(IntPtr intPtr, int num)
        {
            return new BufferStruct(intPtr, num, IsWindows);
        }


        public int Run(LoopHandle loopHandle, int mode)
        {
            return CheckError(_uv_run(loopHandle, mode));
        }

        public void Close(HandleBase handleBase, Close_Callback close_cb)
        {
            _uv_close(handleBase.InternalGetHandle(), close_cb);
        }

        public void UvClose(IntPtr intPtr, Close_Callback close_cb)
        {
            if (intPtr == IntPtr.Zero)
            {
                return;
            }
            _uv_close(intPtr, close_cb);
        }

        /// <summary>
        /// 禁用 nagel 算法以后, 允许很小的包没有延迟立即发送
        /// </summary>
        /// <param name="listenHandle"></param>
        /// <param name="flag"></param>
        public void TcpNodealy(ListenHandle listenHandle, bool flag)
        {
            CheckError(_uv_tcp_nodelay(listenHandle, flag ? 1 : 0));
        }

        public void TcpInit(LoopHandle loopHandle, ListenHandle listenHandle)
        {
            CheckError(_uv_tcp_init(loopHandle, listenHandle));
        }

        public void PipeBind(UvPipeHandle uvPipeHandle, string text)
        {
            CheckError(_uv_pipe_bind(uvPipeHandle, text));
        }

        public void Accept(UvStreamHandle uvStreamHandle, UvStreamHandle l2)
        {
            CheckError(_uv_accept(uvStreamHandle, l2));
        }

  

        public void AsyncInit(LoopHandle loopHandle, AsyncHandle a, AsyncInit_callback asyncInitCallback)
        {
            CheckError(_uv_async_init(loopHandle, a, asyncInitCallback));
        }

        public void TcpBind(ListenHandle listenHandle, ref Addr addr, int flags)
        {
            CheckError(_uv_tcp_bind(listenHandle, ref addr, flags));
        }

        public void PipeInit(LoopHandle loopHandle, UvPipeHandle uvPipeHandle, bool flag)
        {
            CheckError(_uv_pipe_init(loopHandle, uvPipeHandle, flag ? 1 : 0));//最后一个参数为ipc
            //The ipc argument is a boolean to indicate if this pipe will be used for handle passing between processes.
        }

        public void Listen(UvStreamHandle uvStreamHandle, int backlog, Listen_Callback cb)
        {
            CheckError(_uv_listen(uvStreamHandle, backlog, cb));
        }

        public void ReadStart(UvStreamHandle uvStreamHandle, Alloc_Callback alloc_cb, Read_Callback read_cb)
        {
            CheckError(_uv_read_start(uvStreamHandle, alloc_cb, read_cb));
        }

        public int TryWrite(UvStreamHandle uvStreamHandle, BufferStruct[] bufs, int nbufs)
        {
            return CheckError(_uv_try_write(uvStreamHandle, bufs, nbufs));
        }

        public void ShutDown(ShutdownHandle shutdownHandle, UvStreamHandle uvStreamHandle, ShutDown_Callback cb)
        {
            CheckError(_uv_shutdown(shutdownHandle, uvStreamHandle, cb));
        }

        public void Walk(LoopHandle loopHandle, Walk_Callback walk_cb, IntPtr arg)
        {
            _uv_walk(loopHandle, walk_cb, arg);
        }

        public void PipeConnect(UvPipeStream uvPipeStream, UvPipeHandle uvPipeHandle, string text, PipeConnect_Callback pipeConnectCallback)
        {
            _uv_pipe_connect(uvPipeStream, uvPipeHandle, text, pipeConnectCallback);
        }

        public int Ip4Address(string ip, int port, out Addr ptr, out Exception ptr2)
        {
            return GetException(_uv_ip4_addr(ip, port, out ptr), out ptr2);
        }

        public int Ip6Address(string ip, int port, out Addr ptr, out Exception ptr2)
        {
            return GetException(_uv_ip6_addr(ip, port, out ptr), out ptr2);
        }

        public unsafe void Write(WriteHandle writeHandle, UvStreamHandle uvStreamHandle, BufferStruct* bufs, int nbufs, Write_Callback cb)
        {
            CheckError(_uv_write(writeHandle, uvStreamHandle, bufs, nbufs, cb));
        }

        public unsafe void Write2(WriteHandle writeHandle, UvStreamHandle uvStreamHandle, BufferStruct* bufs, int nbufs, UvStreamHandle sendHandle, Write_Callback cb)
        {
            CheckError(_uv_write2(writeHandle, uvStreamHandle, bufs, nbufs, sendHandle, cb));
        }

  
       

        public void Stop(LoopHandle loopHandle)
        {
            _uv_stop(loopHandle);
        }

        public unsafe IPEndPoint GetRemoteEndPoint(UvStreamHandle uvStreamHandle)
        {
            Addr addr = default(Addr);
            int num = Marshal.SizeOf(typeof(Addr));
            CheckError(_uv_tcp_getpeername(uvStreamHandle, out addr, out num));
            byte* ptr = (byte*)(&addr);
            ushort port;
            uint num2;
            checked
            {
                port = (ushort)(((int)ptr[2] << 8) + (int)ptr[3]);
                num2 = (uint)(*(ushort*)(ptr + 6));
                num2 = (num2 << 16) + (uint)(*(ushort*)(ptr + 4));
            }
            return new IPEndPoint((long)((ulong)num2), (int)port);
        }

       

        public int GetUvLoopSize()
        {
            return _uv_loop_size();
        }

        

        public int CheckError(int num)
        {
            Exception ex;
            GetException(num, out ex);
            if (ex != null)
            {
                throw ex;
            }
            return num;
        }

        public string GetErrDescription(int num)
        {
            IntPtr intPtr = _uv_err_name(num);
            if (!(intPtr == IntPtr.Zero))
            {
                return Marshal.PtrToStringAnsi(intPtr);
            }
            return null;
        }

        public string GetStrError(int num)
        {
            IntPtr intPtr = _uv_strerror(num);
            if (!(intPtr == IntPtr.Zero))
            {
                return Marshal.PtrToStringAnsi(intPtr);
            }
            return null;
        }

        #region 加载Lib API
        public Func<IntPtr, bool> FreeLibrary;

        public Func<IntPtr, string, IntPtr> GetProcAddress;

        public bool IsDarwin;//MacOSX 

        public bool IsWindows;//是否是windows系统

        public Func<string, IntPtr> LoadLibrary;
        #endregion

        #region 注入 必须要是变量的形式 否则会被垃圾回收器回收
        private uv_accept _uv_accept;

        private uv_async_init _uv_async_init;

        private uv_async_send _uv_async_send;

        private uv_close _uv_close;

        private uv_err_name _uv_err_name;

        private uv_handle_size _uv_handle_size;

        private uv_ip4_addr _uv_ip4_addr;

        private uv_ip6_addr _uv_ip6_addr;

        private uv_listen _uv_listen;

        private uv_loop_close _uv_loop_close;

        private uv_loop_init _uv_loop_init;

        private uv_loop_size _uv_loop_size;

        private uv_pipe_bind _uv_pipe_bind;

        private uv_pipe_connect _uv_pipe_connect;

        private uv_pipe_init _uv_pipe_init;

        private uv_pipe_pending_count _uv_pipe_pending_count;

        private uv_read_start _uv_read_start;

        private uv_read_stop _uv_read_stop;

        private uv_ref _uv_ref;

        private uv_req_size _uv_req_size;

        private uv_run _uv_run;

        private uv_shutdown _uv_shutdown;

        private uv_stop _uv_stop;

        private uv_strerror _uv_strerror;

        private uv_tcp_bind _uv_tcp_bind;

        private uv_tcp_getpeername _uv_tcp_getpeername;

        private uv_tcp_getsockname _uv_tcp_getsockname;

        private uv_tcp_init _uv_tcp_init;

        private uv_tcp_nodelay _uv_tcp_nodelay;

        private uv_try_write _uv_try_write;

        private uv_unref _uv_unref;

        private uv_walk _uv_walk;

        private uv_write _uv_write;

        private uv_write2 _uv_write2;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_loop_init(LoopHandle loopHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_loop_close(IntPtr i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_run(LoopHandle loopHandle, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void uv_stop(LoopHandle loopHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void uv_ref(HandleBase handleBase);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void uv_unref(HandleBase handleBase);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void uv_close(IntPtr i, Close_Callback closeCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_async_init(LoopHandle loopHandle, AsyncHandle a, AsyncInit_callback asyncInitCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_async_send(AsyncHandle a);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_tcp_nodelay(ListenHandle x, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_tcp_init(LoopHandle loopHandle, ListenHandle x);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_tcp_bind(ListenHandle listenHandle, ref Addr addr, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_pipe_init(LoopHandle loopHandle, UvPipeHandle uvPipeHandle, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate int uv_pipe_bind(UvPipeHandle uvPipeHandle, string s);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate void uv_pipe_connect(UvPipeStream uvPipeStream, UvPipeHandle uvPipeHandle, string s, PipeConnect_Callback pipeConnectCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        protected delegate int uv_pipe_pending_count(UvPipeHandle uvPipeHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_listen(UvStreamHandle uvStreamHandle, int i, Listen_Callback l2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_accept(UvStreamHandle server, UvStreamHandle client);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_read_start(UvStreamHandle uvStreamHandle, Alloc_Callback allocCallback, Read_Callback readCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_read_stop(UvStreamHandle uvStreamHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_try_write(UvStreamHandle uvStreamHandle, BufferStruct[] bufferStruct, int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate int uv_write(WriteHandle writeHandle, UvStreamHandle uvStreamHandle, BufferStruct* bufferStruct, int i, Write_Callback writeCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate int uv_write2(WriteHandle x, UvStreamHandle l1, BufferStruct* x1, int i, UvStreamHandle l2, Write_Callback writeCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_shutdown(ShutdownHandle shutdownHandle, UvStreamHandle uvStreamHandle, ShutDown_Callback shutDownCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr uv_err_name(int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr uv_strerror(int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_loop_size();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_handle_size(HandleType handleType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_req_size(RequestType requestType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_ip4_addr(string s, int i, out Addr addr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_ip6_addr(string s, int i, out Addr addr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_tcp_getsockname(UvStreamHandle uvStreamHandle, out Addr addr, out int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_tcp_getpeername(UvStreamHandle uvStreamHandle, out Addr addr, out int i);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int uv_walk(LoopHandle loopHandle, Walk_Callback walkCallback, IntPtr i);

        #region C#中传委托给C中的函数指针
        //Walk_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Walk_Callback(IntPtr i1, IntPtr i2);

        //ShutDown_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ShutDown_Callback(IntPtr i, int i2);

        //Write Write2 callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Write_Callback(IntPtr i1, int i2);

        //Alloc_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Alloc_Callback(IntPtr i1, int i2, out BufferStruct bufferStruct);

        //Read_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Read_Callback(IntPtr i1, int i2, ref BufferStruct bufferStruct);

        //Listen_allback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Listen_Callback(IntPtr i1, int i2);

        //AsyncInit_callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AsyncInit_callback(IntPtr i);

        //Close_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Close_Callback(IntPtr i);

        //UvPipeConnect_Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PipeConnect_Callback(IntPtr i, int i2);

        #endregion
        #endregion


        /// <summary>
        /// 将Ip 转成uv BSD 能识别的
        /// </summary>
        public struct Addr
        {
            private long a;

            private long b;

            private long c;

            private long d;
        }

        public struct BufferStruct
        {
            public BufferStruct(IntPtr @base, int length, bool flag)
            {
                if (flag)
                {
                    this.@base = (IntPtr)length;
                    this.length = @base;
                    return;
                }
                this.@base = @base;
                this.length = (IntPtr)length;
            }

            private readonly IntPtr @base;

            private readonly IntPtr length;
        }

        public enum HandleType
        {
            UV_UNKNOWN_HANDLE,
            UV_ASYNC,
            UV_CHECK,
            UV_FS_EVENT,
            UV_FS_POLL,
            UV_HANDLE,
            UV_IDLE,
            UV_NAMED_PIPE,
            UV_POLL,
            UV_PREPARE,
            UV_PROCESS,
            UV_STREAM,
            UV_TCP,
            UV_TIMER,
            UV_TTY,
            UV_UDP,
            UV_SIGNAL,
            UV_FILE,
            UV_HANDLE_TYPE_PRIVATE,
            UV_HANDLE_TYPE_MAX
        }

        public enum RequestType
        {
            UV_UNKNOWN_REQ,
            UV_REQ,
            UV_CONNECT,
            UV_WRITE,
            UV_SHUTDOWN,
            UV_UDP_SEND,
            UV_FS,
            UV_WORK,
            UV_GETADDRINFO,
            UV_GETNAMEINFO
        }
    }
}
