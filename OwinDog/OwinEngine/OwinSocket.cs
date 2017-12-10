using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using Model;
using Service;

namespace OwinEngine
{
    public sealed class OwinSocket : ISocket
    {
        private ListenHandle _clientHandle;

        private readonly IPEndPoint _remoteEndPoint;

        private readonly IPEndPoint _localEndPoint;

        private MemoryInfoUtil.MemoryInfo _memoryInfo;

        private bool _closeing;

        private int _actionIndex;

        private readonly Action<Action<object>, object> _postAsync;
        internal OwinSocket(ListenHandle handle, Action<Action<object>, object> action)
        {
            _postAsync = action;//AsyncSendUserPostAction
            _clientHandle = handle;
            _remoteEndPoint = _clientHandle.LibUv.GetRemoteEndPoint(_clientHandle);
            _localEndPoint = _clientHandle.LibUv.GetIpEndPoint(_clientHandle);
        }

        /// <summary>
        /// 获取远程Ip地址
        /// </summary>
        /// <returns></returns>
        public string GetRemoteIpAddress()
        {
            return _remoteEndPoint.Address.ToString();
        }

        /// <summary>
        /// 获取远程访问端口
        /// </summary>
        /// <returns></returns>
        public int GetRemoteIpPort()
        {
            return _remoteEndPoint.Port;
        }

        /// <summary>
        /// 获取本地Ip地址
        /// </summary>
        /// <returns></returns>
        public string LocalIpAddress()
        {
            return _localEndPoint.Address.ToString();
        }

        /// <summary>
        /// 获取本地访问端口
        /// </summary>
        /// <returns></returns>
        public int LocalIpPort()
        {
            return _localEndPoint.Port;
        }


        #region 读操作
        public void Read(Action<OwinSocket, byte[], int, Exception, object> callBack, object state)
        {
            CheckClosed();
            ReadState readState = new ReadState
            {
                Callback = callBack,
                OtheState = state
            };
            if (Thread.CurrentThread.ManagedThreadId != _clientHandle.Loop.LoopRunThreadId)
            {
                //如果不是libuv主线程
                _postAsync(new Action<object>(ReadForPost), readState);
                return;
            }
            ReadForPost(readState);
        }
        private void ReadForPost(object state)
        {
            ReadState readState = (ReadState)state;
            try
            {
                _actionIndex = ActionStoreManage.Add(new Action(ReadStop));
                _clientHandle.Read(new Func<UvStreamHandle, int, object, LibUv.BufferStruct>(AllocCallback),
                    new Action<UvStreamHandle, int, Exception, object>(ReadCallback), readState);
            }
            catch (Exception error)
            {
                ActionStoreManage.Remove(_actionIndex, new Action(ReadStop));
                _actionIndex = 0;
                readState.Callback(this, null, -1, error, readState.OtheState);
            }
        }

        /// <summary>
        /// 开辟了一个新的缓冲区来容纳新到来的数据.
        /// </summary>
        /// <param name="handle">clientHandle</param>
        /// <param name="suggestedSize">开辟的长度</param>
        /// <param name="state">回调参数</param>
        /// <returns></returns>
        private LibUv.BufferStruct AllocCallback(UvStreamHandle handle, int suggestedSize, object state)
        {
            if (_memoryInfo == null)
            {
                _memoryInfo = MemoryInfoUtil.GetMemoryInfo();
            }
            return handle.LibUv.CreateBufferStruct(_memoryInfo.AddrOfBuffer, _memoryInfo.Buffer.Length);
        }

        /// <summary>
        /// libuv读取成功后回调函数
        /// </summary>
        /// <param name="handle">clientHandle</param>
        /// <param name="nread">读取的长度</param>
        /// <param name="error">Exception</param>
        /// <param name="state">回调参数</param>
        private void ReadCallback(UvStreamHandle handle, int nread, Exception error, object state)
        {
            if (_actionIndex > 0)
            {
                ActionStoreManage.Remove(_actionIndex, new Action(ReadStop));
                _actionIndex = 0;
            }
            bool flag = error == null && nread > 0;
            //bool flag2 = nread == 0 || nread == -4077 || nread == -4095;
            ReadState readState = (ReadState)state;
            try
            {
                if (flag)
                {
                    readState.Callback(this, _memoryInfo.Buffer, nread, null, readState.OtheState);
                }
                else
                {
                    readState.Callback(this, null, -1, error, readState.OtheState);
                }
            }
            finally
            {
                handle.Stop();
                //AllocCallback 开辟的内存空间回收
                RecoverMemoryInfo();
            }
        }
        #endregion

        #region 写操作
        public void Write(byte[] array, Action<OwinSocket, int, Exception, object> callback, object otherState)
        {
            CheckClosed();
            WriteState writeStateForCallBk = new WriteState
            {
                Callback = callback,
                OtherState = otherState
            };
            WriteStateForPost writeStateForPost = new WriteStateForPost
            {
                WriteObject = writeStateForCallBk,
                Buffers = new List<byte[]>
                {
                    array
                }
            };
            if (Thread.CurrentThread.ManagedThreadId != _clientHandle.Loop.LoopRunThreadId)
            {
                _postAsync(new Action<object>(WriteForPost), writeStateForPost);
                return;
            }
            WriteForPost(writeStateForPost);
        }
        public void WriteForPost(byte[] headDomain, byte[] body, Action<OwinSocket, int, Exception, object> callback, object otherState)
        {
            CheckClosed();
            WriteState writeState = new WriteState
            {
                Callback = callback,
                OtherState = otherState
            };
            WriteStateForPost writeStateForPost = new WriteStateForPost
            {
                WriteObject = writeState,
                Buffers = new List<byte[]>
                {
                    headDomain,
                    body
                }
            };
            if (Thread.CurrentThread.ManagedThreadId != _clientHandle.Loop.LoopRunThreadId)
            {
                _postAsync(new Action<object>(WriteForPost), writeStateForPost);
                return;
            }
            WriteForPost(writeStateForPost);
        }

        private void WriteForPost(object state)
        {
            WriteStateForPost writeStateForPost = (WriteStateForPost)state;
            WriteHandle writeHandle = new WriteHandle();
            writeHandle.Init(_clientHandle.Loop);
            List<byte[]> buffers = writeStateForPost.Buffers;
            try
            {
                if (buffers.Count == 1)
                {
                    writeHandle.Write(_clientHandle, buffers[0], new Action<int, Exception, object>(WriteCallback), writeStateForPost.WriteObject);
                }
                else if (buffers.Count == 2)
                {
                    writeHandle.Write(_clientHandle, buffers[0], buffers[1], new Action<int, Exception, object>(WriteCallback), writeStateForPost.WriteObject);
                }
            }
            catch (Exception ex)
            {
                WriteCallback(-1, ex, writeStateForPost.WriteObject);
            }
        }
      
        private void WriteCallback(int status, Exception ex, object otherState)
        {
            WriteState writeState = (WriteState)otherState;
            try
            {
                writeState.Callback(this, status, ex, writeState.OtherState);
            }
            catch (Exception value)
            {
                Console.WriteLine("DEBUG: UvSocket.WriteCallback Error:");
                Console.WriteLine(value);
            }
        }

        #endregion





 
       

        private void Dispose(object obj)
        {
            Close();
        }

        public void Dispose()
        {
            if (_clientHandle == null || _clientHandle.IsClosed || _closeing)
            {
                return;
            }
            _closeing = true;
            if (Thread.CurrentThread.ManagedThreadId != _clientHandle.Loop.LoopRunThreadId)
            {
                var action = new Action<object>(Dispose);
                _postAsync(action, null);
                return;
            }
            Close();
        }

        private void ReadStop()
        {
            _actionIndex = 0;
            if (_clientHandle == null)
            {
                return;
            }
            _clientHandle.ReadStop();
        }

        #region 关闭操作
        private void Close()
        {
            if (_actionIndex > 0)
            {
                ActionStoreManage.Remove(_actionIndex, new Action(ReadStop));
            }
            _actionIndex = 0;
            _closeing = true;
            RecoverMemoryInfo();
            ShutdownHandle shutdownHandle = new ShutdownHandle();
            shutdownHandle.Init(_clientHandle.Loop);
            shutdownHandle.ShutDown(_clientHandle, new Action<int, object>(CloseCallBack), null);
        }
        private void CloseCallBack(int num, object obj)
        {
            if (_clientHandle != null)
            {
                _clientHandle.Dispose();
            }
            _clientHandle = null;
            _closeing = false;
        } 
        #endregion

        /// <summary>
        /// 检测是否关闭
        /// </summary>
        private void CheckClosed()
        {
            if (_clientHandle == null || _clientHandle.IsClosed || _closeing)
            {
                throw new Exception("OwinDog Socket Closed.");
            }
        }

        /// <summary>
        /// 开辟的内存空间Buffer回收利用
        /// </summary>
        private void RecoverMemoryInfo()
        {
            if (_memoryInfo != null)
            {
                MemoryInfoUtil.AddMemoryInfo(_memoryInfo);
            }
            _memoryInfo = null;
        }

        ~OwinSocket()
        {
            if (_clientHandle != null)
            {
                Dispose();
            }
            if (_memoryInfo != null)
            {
                RecoverMemoryInfo();
            }
        }


        private class ReadState
        {
            public Action<OwinSocket, byte[], int, Exception, object> Callback;

            public object OtheState;
        }

        private class WriteState
        {
            public Action<OwinSocket, int, Exception, object> Callback;

            public object OtherState;
        }

        private class WriteStateForPost
        {
            public WriteState WriteObject;
            public List<byte[]> Buffers;
        }

        private static class MemoryInfoUtil
        {
            public static MemoryInfo GetMemoryInfo()
            {
                
                MemoryInfo result;
                lock (MemoryInfQueue)
                {
                    if (MemoryInfQueue.Count < 1)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            byte[] array = new byte[BufferSize];
                            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                            MemoryInfo item = new MemoryInfo
                            {
                                Buffer = array,
                                Handle = handle,
                                AddrOfBuffer = handle.AddrOfPinnedObject()
                            };
                            MemoryInfQueue.Enqueue(item);
                        }
                    }
                    result = MemoryInfQueue.Dequeue();
                }
                return result;
            }

            public static void AddMemoryInfo(MemoryInfo _memoryInfo)
            {
                lock (MemoryInfQueue)
                {
                    if (MemoryInfQueue.Count > QueMax)
                    {
                        _memoryInfo.Handle.Free();
                    }
                    else
                    {
                        MemoryInfQueue.Enqueue(_memoryInfo);
                    }
                }
            }

            private const int BufferSize = 4096;

            private const int QueMax = 4096;

            private static readonly Queue<MemoryInfo> MemoryInfQueue = new Queue<MemoryInfo>(1024);
            internal class MemoryInfo 
            {
                public IntPtr AddrOfBuffer;

                public byte[] Buffer;

                public GCHandle Handle;//Buffer的指针
            }
        }

    }


   


}
