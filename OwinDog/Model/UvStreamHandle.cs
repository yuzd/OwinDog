using System;
using System.Runtime.InteropServices;

namespace Model
{
    public abstract class UvStreamHandle : HandleBase
    {
        private static readonly LibUv.Listen_Callback _Listen_Callback = new LibUv.Listen_Callback(UvConnectionCb);

        private static readonly LibUv.Alloc_Callback _Alloc_Callback = new LibUv.Alloc_Callback(Alloc_Callback);

        private static readonly LibUv.Read_Callback _Read_Callback = new LibUv.Read_Callback(UbReadCb);


        public Action<UvStreamHandle, int, Exception, object> _connectionCB;

        public object _listenState;

        private GCHandle _listenVitality;

        public Func<UvStreamHandle, int, object, LibUv.BufferStruct> _allocCallback;

        public Action<UvStreamHandle, int, Exception, object> _readCallback;

        public object _readState;

        private GCHandle _GCHandle;
        public void ReadStop()
        {
            if (!_GCHandle.IsAllocated)
            {
                return;
            }
            LibUv.ReadStop(this);
            LibUv.BufferStruct bufferStruct = LibUv.CreateBufferStruct(IntPtr.Zero, 0);
            UbReadCb(handle, 0, ref bufferStruct);
        }

        public void Stop()
        {
            if (!_GCHandle.IsAllocated)
            {
                return;
            }
            _allocCallback = null;
            _readCallback = null;
            _readState = null;
            if (_GCHandle.IsAllocated)
            {
                _GCHandle.Free();
            }
            LibUv.ReadStop(this);
        }

        public void Accept(UvStreamHandle uvStreamHandle)
        {
            LibUv.Accept(this, uvStreamHandle);
        }
      
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hdl">队列长度</param>
        /// <param name="connectionCallBack"></param>
        /// <param name="state"></param>
        public void Listen(int hdl, Action<UvStreamHandle, int, Exception, object> connectionCallBack, object state)
        {
            if (_listenVitality.IsAllocated)//如果分配了句柄,则为 true;否则为 false
            {
                throw new InvalidOperationException("TODO: Listen may not be called more than once");
            }
            try
            {
                _connectionCB = connectionCallBack;
                _listenState = state;
                _listenVitality = GCHandle.Alloc(this, GCHandleType.Normal);
                LibUv.Listen(this, hdl, _Listen_Callback);
            }
            catch
            {
                _connectionCB = null;
                _listenState = null;
                if (_listenVitality.IsAllocated)
                {
                    _listenVitality.Free();
                }
                throw;
            }
        }
        private static void UvConnectionCb(IntPtr intPtr, int num)
        {
            UvStreamHandle uvStreamHandle = GetObjectFromHandel<UvStreamHandle>(intPtr);
            Exception arg;
            num = uvStreamHandle.LibUv.GetException(num, out arg);
            try
            {
                uvStreamHandle._connectionCB(uvStreamHandle, num, arg, uvStreamHandle._listenState);
            }
            catch (Exception value)
            {
                Console.WriteLine("DEBUG: UvStreamHandle.UvConnectionCb Call User Function Error:");
                Console.WriteLine(value);
            }
        }
       
        public void Read(Func<UvStreamHandle, int, object, LibUv.BufferStruct> allocCallback, Action<UvStreamHandle, int, Exception, object> readCallback, object obj)
        {
            if (_GCHandle.IsAllocated)
            {
                throw new InvalidOperationException("UvStreamHandle.cs: ReadStop must be called before ReadStart may be called again");
            }
            try
            {
                _allocCallback = allocCallback;
                _readCallback = readCallback;
                _readState = obj;
                _GCHandle = GCHandle.Alloc(this, GCHandleType.Normal);
                LibUv.ReadStart(this, _Alloc_Callback, _Read_Callback);
            }
            catch
            {
                _allocCallback = null;
                _readCallback= null;
                _readState = null;
                if (_GCHandle.IsAllocated)
                {
                    _GCHandle.Free();
                }
                throw;
            }
        }

        /// <summary>
        /// libuv read 读取 分配内存的回调函数
        /// </summary>
        /// <param name="intPtr">ClientHandle的指针</param>
        /// <param name="suggestedSize">请求内存大小</param>
        /// <param name="buffer"></param>
        private static void Alloc_Callback(IntPtr intPtr, int suggestedSize, out LibUv.BufferStruct buffer)
        {
            UvStreamHandle uvStreamHandle = GetObjectFromHandel<UvStreamHandle>(intPtr);
            try
            {
                buffer = uvStreamHandle._allocCallback(uvStreamHandle, suggestedSize, uvStreamHandle._readState);
            }
            catch (Exception)
            {
                buffer = uvStreamHandle.LibUv.CreateBufferStruct(IntPtr.Zero, 0);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="intPtr"></param>
        /// <param name="num"></param>
        /// <param name="ptr"></param>
        private static void UbReadCb(IntPtr intPtr, int num, ref LibUv.BufferStruct ptr)
        {
            UvStreamHandle uvStreamHandle = GetObjectFromHandel<UvStreamHandle>(intPtr);
            try
            {
                if (num < 0)
                {
                    Exception arg;
                    uvStreamHandle.LibUv.GetException(num, out arg);
                    uvStreamHandle._readCallback(uvStreamHandle, 0, arg, uvStreamHandle._readState);
                }
                else
                {
                    uvStreamHandle._readCallback(uvStreamHandle, num, null, uvStreamHandle._readState);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(">>> DEBUG: OwinDog.LibuvApi.UvStreamHandle.UbReadCb: Call UvSocket  function error:");
                Console.WriteLine(ex.ToString());
            }
        }

        protected override bool ReleaseHandle()
        {
            if (_listenVitality.IsAllocated)
            {
                _listenVitality.Free();
            }
            if (_GCHandle.IsAllocated)
            {
                _GCHandle.Free();
            }
            return base.ReleaseHandle();
        }


    }
   
}
