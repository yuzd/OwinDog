using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Service;
namespace OwinEngine
{
    public sealed class OwinStream : Stream
    {

        private readonly RequestData _requestData;

        private bool _socketError;

        private bool _closeed;

        private static readonly SimpleThreadPool _simpleThreadPool;

        private bool _disposed;

        static OwinStream()
        {
            int num = checked(Environment.ProcessorCount + 1);
            if (num < 2)
            {
                num = 2;
            }
            if (num > 64)
            {
                num = 64;
            }
            _simpleThreadPool = new SimpleThreadPool(num);
        }

        internal OwinStream(RequestData requestData)
        {
            _requestData = requestData;
        }

        private void CheckDisposed()
        {
            if (_disposed || _closeed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            if (_socketError)
            {
                throw new SocketException(10054);
            }
        }

      
        private static void ExcuteUserCallbackFunc(object state)
        {
            CustomeAsyncResult customeAsyncResult = state as CustomeAsyncResult;
            if (customeAsyncResult == null)
            {
                return;
            }
            if (customeAsyncResult.UserCallbackFunc == null)
            {
                return;
            }
            customeAsyncResult.UserCallbackFunc(customeAsyncResult);
        }

        internal IAsyncResult Write(IList<WriteParam> list, AsyncCallback userCallbackFunc, object obj)
        {
            if (_closeed || _disposed || _socketError || list == null)
            {
                if (_socketError)
                {
                    throw new SocketException(10054);
                }
                if (_closeed || _disposed)
                {
                    throw new ObjectDisposedException("Stream");
                }
                if (list == null)
                {
                    throw new ArgumentNullException("buffers");
                }
            }
            CustomeAsyncResult customeAsyncResult = new CustomeAsyncResult(obj) {UserCallbackFunc = (userCallbackFunc)};
            if (list.Count == 1)
            {
                byte[] array = new byte[list[0].Length];
                Buffer.BlockCopy(list[0].Buffer, list[0].Offset, array, 0, array.Length);
                _requestData.Socket.Write(array, new Action<OwinSocket, int, Exception, object>(OnWriteComplete), customeAsyncResult);
                return customeAsyncResult;
            }
            if (list.Count == 2)
            {
                byte[] array2 = new byte[list[0].Length];
                Buffer.BlockCopy(list[0].Buffer, list[0].Offset, array2, 0, array2.Length);
                byte[] array3 = new byte[list[1].Length];
                Buffer.BlockCopy(list[1].Buffer, list[1].Offset, array3, 0, array3.Length);
                _requestData.Socket.WriteForPost(array2, array3, new Action<OwinSocket, int, Exception, object>(OnWriteComplete), customeAsyncResult);
                return customeAsyncResult;
            }
            throw new Exception("count...");
        }

        /// <summary>
        /// 写完后 如果是websocket的话 直接再调用 读 ExcuteUserCallbackFunc writehandle 接口 回掉
        /// </summary>
        /// <param name="owinSocket"></param>
        /// <param name="num"></param>
        /// <param name="ex"></param>
        /// <param name="obj"></param>
        private void OnWriteComplete(OwinSocket owinSocket, int num, Exception ex, object obj)
        {
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)obj;
            if (num != 0 || ex != null)
            {
                _socketError = true;
                customeAsyncResult.SocketIsErrOrClose = (true);
            }
            customeAsyncResult.IsCompleted = (true);
            ((AutoResetEvent)customeAsyncResult.AsyncWaitHandle).Set();
            if (customeAsyncResult.UserCallbackFunc != null)
            {
                _simpleThreadPool.UnsafeQueueUserWorkItem(new Action<object>(ExcuteUserCallbackFunc), customeAsyncResult);
            }
        }

        /// <summary>
        /// 调用libuv读数据后的回调方法 websocket情况下 此时的 ExcuteUserCallbackFunc 的参数 customeAsyncResult 的 UserCallbackFunc 开始解析
        /// </summary>
        /// <param name="sck"></param>
        /// <param name="data"></param>
        /// <param name="nread"></param>
        /// <param name="e"></param>
        /// <param name="state"></param>
        private void OnReadComplite(OwinSocket sck, byte[] data, int nread, Exception e, object state)
        {
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)state;
            if (nread < 1 || data == null || e != null)
            {
                customeAsyncResult.SocketIsErrOrClose = (true);
                customeAsyncResult.RealRecvSize = -1;
                _socketError = true;
            }
            else
            {
                int realRecvSize = Math.Min(nread, customeAsyncResult.RecvLength);
                Buffer.BlockCopy(data, 0, customeAsyncResult.RecvBuffer, customeAsyncResult.RecvOffset, realRecvSize );
                customeAsyncResult.RealRecvSize = realRecvSize ;
                if (realRecvSize  < nread)
                {
                    int num3 = checked(nread - realRecvSize );
                    _requestData._preLoadedBody = new byte[num3];
                    Buffer.BlockCopy(data, realRecvSize , _requestData._preLoadedBody, 0, num3);
                }
            }
            customeAsyncResult.IsCompleted = (true);
            ((AutoResetEvent)customeAsyncResult.AsyncWaitHandle).Set();
            if (customeAsyncResult.UserCallbackFunc != null)
            {
                _simpleThreadPool.UnsafeQueueUserWorkItem(new Action<object>(ExcuteUserCallbackFunc), customeAsyncResult);
            }
        }


        public override IAsyncResult BeginRead(byte[] recvBuffer, int recvOffset, int num, AsyncCallback userCallbackFunc, object obj)
        {
            OwinAsyncState owinAsyncState = new OwinAsyncState();
            owinAsyncState.OwinStream = this;
            CheckDisposed();
            CustomeAsyncResult customeAsyncResult = new CustomeAsyncResult(obj)
            {
                RecvBuffer = (recvBuffer),
                RecvOffset = (recvOffset),
                RecvLength = (num),
                UserCallbackFunc = (userCallbackFunc)
            };
            if (_requestData._preLoadedBody != null && _requestData._preLoadedBody.Length > 0)
            {
              
                int num3 = Math.Min(num, _requestData._preLoadedBody.Length);
                Buffer.BlockCopy(_requestData._preLoadedBody, 0, customeAsyncResult.RecvBuffer, customeAsyncResult.RecvOffset, num3);
                int num4 = (_requestData._preLoadedBody.Length - num3);
                if (num4 < 1)
                {
                    Array.Resize<byte>(ref _requestData._preLoadedBody, 0);
                    _requestData._preLoadedBody = null;
                }
                else
                {
                    byte[] array2 = new byte[num4];
                    Buffer.BlockCopy(_requestData._preLoadedBody, num3, array2, 0, num4);
                    Array.Resize<byte>(ref _requestData._preLoadedBody, num4);
                    Buffer.BlockCopy(array2, 0, _requestData._preLoadedBody, 0, num4);
                }
                customeAsyncResult.RealRecvSize = num3;
                customeAsyncResult.IsCompleted = (true);
                ((AutoResetEvent)customeAsyncResult.AsyncWaitHandle).Set();
                if (customeAsyncResult.UserCallbackFunc != null)
                {
                    _simpleThreadPool.UnsafeQueueUserWorkItem(new Action<object>(ExcuteUserCallbackFunc), customeAsyncResult);
                }
                return customeAsyncResult;
            }
            owinAsyncState.CustomeAsyncResult = customeAsyncResult;//
            ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(owinAsyncState.CallBack), null);
            return owinAsyncState.CustomeAsyncResult;
        }

        public override IAsyncResult BeginWrite(byte[] array, int num, int num2, AsyncCallback asyncCallback, object obj)
        {
            if (_closeed || _disposed || _socketError || num2 < 1)
            {
                if (_socketError)
                {
                    throw new SocketException(10064);
                }
                if (_closeed || _disposed)
                {
                    throw new ObjectDisposedException("Stream");
                }
                if (num2 < 1)
                {
                    throw new ArgumentOutOfRangeException("count");
                }
            }
            return Write(new WriteParam[]
			{
				new WriteParam
				{
					Buffer = array,
					Offset = num,
					Length = num2
				}
			}, asyncCallback, obj);
        }

        public override void Close()
        {
            _closeed = true;
        }

        protected override void Dispose(bool flag)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _closeed = true;
            if (flag)
            {
                GC.SuppressFinalize(this);
            }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            CustomeAsyncResult customeAsyncResult = asyncResult as CustomeAsyncResult;
            if (customeAsyncResult == null)
            {
                throw new ArgumentNullException("asyncResult");
            }
            AutoResetEvent autoResetEvent = asyncResult.AsyncWaitHandle as AutoResetEvent;
            if (autoResetEvent == null)
            {
                throw new Exception("'IAsyncResult.AsyncWaitHandle' is null.");
            }
            if (!customeAsyncResult.IsCompleted)
            {
                autoResetEvent.WaitOne();
            }
            return customeAsyncResult.RealRecvSize;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (!asyncResult.IsCompleted)
            {
                asyncResult.AsyncWaitHandle.WaitOne();
            }
        }


        public override int Read(byte[] array, int num, int num2)
        {
            if (num2 < 1 || checked(num + num2) > array.Length)
            {
                return 0;
            }
            IAsyncResult asyncResult = BeginRead(array, num, num2, null, null);
            return EndRead(asyncResult);
        }

        public override int ReadByte()
        {
            byte[] array = new byte[1];
            int num = Read(array, 0, 1);
            if (num < 1)
            {
                return -1;
            }
            return (int)array[0];
        }
        public override void Write(byte[] buffer, int offset, int num)
        {
            if (num < 1)
            {
                return;
            }
            EndWrite(BeginWrite(buffer, offset, num, null, null));
        }

        public override void WriteByte(byte b)
        {
            byte[] buffer = new byte[]
            {
                b
            };
            Write(buffer, 0, 1);
        }

        public override long Seek(long num, SeekOrigin seekOrigin)
        {
            throw new NotSupportedException();
        }

        ~OwinStream()
        {
            Dispose(false);
        }

        public override void Flush()
        {
        }

        public override bool CanRead
        {
            get
            {
                return !_socketError && !_closeed;
            }
        }



        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }



        public override bool CanTimeout
        {
            get
            {
                CheckDisposed();
                return !_socketError && !_closeed;
            }
        }


        public override bool CanWrite
        {
            get
            {
                return !_socketError && !_closeed;
            }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }


        public override long Position { get; set; }


        public override int ReadTimeout { get; set; }

        public override int WriteTimeout { get; set; }

        public override void SetLength(long num)
        {
            throw new NotSupportedException();
        }

        

     


        private sealed class OwinAsyncState
        {
            /// <summary>
            /// 调用libuv进行读取数据 然后回调方法
            /// </summary>
            /// <param name="obj"></param>
            public void CallBack(object obj)
            {
                OwinStream._requestData.Socket.Read(new Action<OwinSocket, byte[], int, Exception, object>(OwinStream.OnReadComplite), CustomeAsyncResult);
            }

            public CustomeAsyncResult CustomeAsyncResult;

            public OwinStream OwinStream;
        }
    }
}
