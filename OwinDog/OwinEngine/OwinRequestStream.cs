using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Service;

namespace OwinEngine
{
    internal class OwinRequestStream : Stream
    {
        private readonly Stream _base;

        private int _nextLength;

        public OwinRequestStream(Stream baseStream, int maxReuestLength)
        {
            _base = baseStream;
            _nextLength = maxReuestLength;
        }


        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object state)
        {
            if (_nextLength < 1)
            {
                CustomeAsyncResult customeAsyncResult = new CustomeAsyncResult(state);
                customeAsyncResult.RealRecvSize= (-1);
                customeAsyncResult.IsCompleted = (true);
                customeAsyncResult.RecvBuffer=(buffer);
                customeAsyncResult.RecvLength=(count);
                customeAsyncResult.RecvOffset=(offset);
                customeAsyncResult.UserCallbackFunc=(asyncCallback);
                ((AutoResetEvent)customeAsyncResult.AsyncWaitHandle).Set();
                if (asyncCallback != null)
                {
                    asyncCallback.BeginInvoke(customeAsyncResult, null, null);
                }
                return customeAsyncResult;
            }
            return _base.BeginRead(buffer, offset, count, asyncCallback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object state)
        {
            throw new NotSupportedException();
        }

        public override void Close()
        {
        }

        protected override void Dispose(bool disposing)
        {
            _base.Dispose();
            base.Dispose(disposing);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)asyncResult;
            if (customeAsyncResult.RealRecvSize< 0)
            {
                if (!customeAsyncResult.IsCompleted)
                {
                    customeAsyncResult.IsCompleted = (true);
                    ((AutoResetEvent)customeAsyncResult.AsyncWaitHandle).Set();
                }
                return 0;
            }
            int num = _base.EndRead(asyncResult);
            if (num < 1)
            {
                return 0;
            }
            checked
            {
                _nextLength -= num;
                return num;
            }
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override bool CanRead {
            get { return true; }
        }

       

        public override bool CanSeek {
            get
            {
                return false;
            }
        }

       
        public override bool CanTimeout
        {
            get{
                return _base.CanTimeout;
            }

        }

        public override bool CanWrite
        {
            get { throw new NotImplementedException(); }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }


        public override long Position { get; set; }

      

        public override int ReadTimeout
        {
            get
            {
                return _base.ReadTimeout;
            }
            set
            {
                _base.ReadTimeout = value;
            }
        }
        

       

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_nextLength < 1)
            {
                return 0;
            }
            int num = _base.Read(buffer, offset, count);
            if (num < 0)
            {
                return 0;
            }
            checked
            {
                _nextLength -= num;
                return num;
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
            try
            {
                BeginRead(buffer, offset, count, new AsyncCallback(ReadAsyncCallBack), taskCompletionSource);
            }
            catch (Exception exception)
            {
                taskCompletionSource.SetException(exception);
            }
            return taskCompletionSource.Task;
        }

       

        public override int ReadByte()
        {
            if (_nextLength < 1)
            {
                return -1;
            }
            return _base.ReadByte();
        }

        public override long Seek(long num, SeekOrigin seekOrigin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long num)
        {
            throw new NotSupportedException();
        }
       
        public override void Write(byte[] array, int num, int num2)
        {
            throw new NotSupportedException();
        }

        public override void WriteByte(byte b)
        {
            throw new NotSupportedException();
        }




        private void ReadAsyncCallBack(IAsyncResult asyncResult)
        {
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)asyncResult;
            TaskCompletionSource<int> taskCompletionSource = (TaskCompletionSource<int>)customeAsyncResult.AsyncState;
            if (customeAsyncResult.SocketIsErrOrClose || customeAsyncResult.SocketIsTimeOut || customeAsyncResult.RealRecvSize < 0)
            {
                taskCompletionSource.SetException(new Exception("socket error."));
                return;
            }
            int num = customeAsyncResult.RealRecvSize;
            if (num < 1)
            {
                num = 0;
            }
            checked
            {
                _nextLength -= num;
                taskCompletionSource.SetResult(num);
            }
        }
    }

}


