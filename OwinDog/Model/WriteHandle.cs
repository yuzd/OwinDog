using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Model
{
    public class WriteHandle : HandleBase
    {
        private const int BUFFER_COUNT = 4;


        private IntPtr _bufs;

        private Action<int, Exception, object> _callback;

        private object _state;

        private readonly List<GCHandle> _pins = new List<GCHandle>();

        private static readonly int _bufferSize;

        private LoopHandle _loopHandle;

        private static readonly IntPtr _bufferPoint;

        private static readonly LibUv.Write_Callback _Write_Callback = new LibUv.Write_Callback(UvWriteCallback);
        static unsafe WriteHandle()
        {
            _bufferSize = (Marshal.SizeOf(typeof(LibUv.BufferStruct)) * BUFFER_COUNT);
            IntPtr value = Marshal.AllocHGlobal(BUFFER_COUNT);//申请4个字节内存
            *(int*)((void*)value) = 134744330;
            _bufferPoint = value;
        }

        public void Init(LoopHandle loopHandle)
        {
            _loopHandle = loopHandle;
            Init(_loopHandle.LibUv, (_loopHandle.LibUv.WriteReqSize + _bufferSize), _loopHandle.LoopRunThreadId);
            _bufs = handle + loopHandle.LibUv.WriteReqSize;
        }
    
        public unsafe void Free(UvStreamHandle uvPipeHandle, UvStreamHandle sendHandle, Action<int, Exception, object> callBack, object state)
        {
            _callback = callBack;
            _state = state;
            try
            {
                _pins.Add(GCHandle.Alloc(this, GCHandleType.Normal));
                LibUv.BufferStruct* ptr = (LibUv.BufferStruct*)((void*)_bufs);
                *ptr = LibUv.CreateBufferStruct(_bufferPoint, 4);
                LibUv.Write2(this, uvPipeHandle, ptr, 1, sendHandle, _Write_Callback);
            }
            catch
            {
                HandleFree(this);
                sendHandle.Dispose();
                _callback = null;
                _state = null;
                throw;
            }
        }
        public void Write(UvStreamHandle uvStreamHandle, byte[] buffer1, Action<int, Exception, object> callBack, object state)
        {
            Write(uvStreamHandle, buffer1, 0, buffer1.Length, null, 0, 0, callBack, state);
        }

        public void Write(UvStreamHandle uvStreamHandle, byte[] array, byte[] array2, Action<int, Exception, object> callBack, object state)
        {
            Write(uvStreamHandle, array, 0, array.Length, array2, 0, (array2 == null) ? 0 : array2.Length, callBack, state);
        }
        
        private unsafe void Write(UvStreamHandle uvStreamHandle, byte[] buffer1, int buffer1StartIndex, int buffer1Offset, byte[] buffer2, int buffer2StartIndex, int buffer2Offset, Action<int, Exception, object> callBack, object state)
        {

            if (buffer1 == null || buffer1Offset < 1 || buffer1.Length < buffer1Offset + buffer1StartIndex)
            {
                throw new ArgumentNullException("buffer1");
            }
            _callback = callBack;
            _state = state;
            try
            {
                _pins.Add(GCHandle.Alloc(this, GCHandleType.Normal));
                LibUv.BufferStruct* ptr = (LibUv.BufferStruct*)((void*)_bufs);
                int nbufs = 0;
                GCHandle item = GCHandle.Alloc(buffer1, GCHandleType.Pinned);
                _pins.Add(item);
                //返回的是对象的“数据区”的起始地址
                *ptr = LibUv.CreateBufferStruct(item.AddrOfPinnedObject() + buffer1StartIndex, buffer1Offset);
                nbufs++;
                if (buffer2 != null && buffer2Offset > 0)
                {
                    if (buffer2.Length < buffer2Offset + buffer2StartIndex)
                    {
                        throw new ArgumentException("buffer2");
                    }
                    item = GCHandle.Alloc(buffer2, GCHandleType.Pinned);
                    _pins.Add(item);
                    ptr[1] = LibUv.CreateBufferStruct(item.AddrOfPinnedObject() + buffer2StartIndex, buffer2Offset);
                    nbufs++;
                }
                //int uv_write(uv_write_t* req, uv_stream_t* handle,uv_buf_t bufs[], int bufcnt, uv_write_cb cb);
                LibUv.Write(this, uvStreamHandle, ptr, nbufs, _Write_Callback);
            }
            catch
            {
                HandleFree(this);
                _callback = null;
                _state = null;
                throw;
            }
        }
        private static void HandleFree(WriteHandle writeHandle)
        {
            if (writeHandle._pins.Count < 1)
            {
                return;
            }
            foreach (GCHandle current in writeHandle._pins)
            {
                current.Free();
            }
            writeHandle._pins.Clear();
        }

        private static void UvWriteCallback(IntPtr intPtr, int status)
        {
            WriteHandle writeHandle = GetObjectFromHandel<WriteHandle>(intPtr);
            Action<int, Exception, object> callback = writeHandle._callback;
            writeHandle._callback = null;
            object state = writeHandle._state;
            writeHandle._state = null;
            Exception arg = null;
            if (status < 0)
            {
                writeHandle.LibUv.GetException(status, out arg);
            }
            try
            {
                callback(status, arg, state);
            }
            catch (Exception ex)
            {
                Console.WriteLine("* UvWriteCallback Error：" + Environment.NewLine + ex.ToString());
            }
            finally
            {
                HandleFree(writeHandle);
                writeHandle.Close();
                writeHandle.Dispose();
            }
        }

       

        protected override bool ReleaseHandle()
        {
            _Close_Callback(handle);
            handle = IntPtr.Zero;
           // Marshal.FreeHGlobal(_bufferPoint);
            return true;
        }

    }

  
}
