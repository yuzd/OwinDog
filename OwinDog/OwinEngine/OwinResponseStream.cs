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
using Util;

namespace OwinEngine
{
    internal class OwinResponseStream : Stream
    {
        private const short CRLF = 2573;

        private const int CRLFCRLF = 168626701;

        private Stream _base;

        /// <summary>
        /// Owin环境
        /// </summary>
        private IDictionary<string, object> _owinEnv;

        private bool _closed;

        /// <summary>
        /// 服务器标志
        /// </summary>
        private static readonly string ServerHendleValue;

        private bool _appDataIsChunked;

        /// <summary>
        /// 回调函数集合
        /// </summary>
        private readonly List<SendHandlesCallBackInfo> SendHandlesCallbacks = new List<SendHandlesCallBackInfo>();

        private bool _rspEndCalled;

        private bool _chunkEndTagWrited;

        private bool _disposed;

        private bool _handleWrited;

        private bool _contentSizeSetted;



        static OwinResponseStream()
        {
            ServerHendleValue = "OwinDog/1.0";
        }

        public OwinResponseStream(IDictionary<string, object> env, Stream stream, bool flag)
        {
            _owinEnv = env;
            _base = stream;
            ConnectionClosed = (!flag);
        }


        private bool HaveResponse()
        {
            IDictionary<string, string[]> dictionary = _owinEnv["owin.ResponseHeaders"] as IDictionary<string, string[]>;
            return _owinEnv.ContainsKey("owin.ResponseStatusCode") ||
                _owinEnv.ContainsKey("owin.ResponseReasonPhrase") ||
                (dictionary != null && dictionary.Any<KeyValuePair<string, string[]>>()) ||
                IsBeginWrite;
        }




        public bool ConnectionClosed { get; set; }
        public bool IsBeginWrite { get; set; }

        private unsafe bool HaveChunkEndTag(byte[] array)
        {
            if (array == null || array.Length < 5)
            {
                return false;
            }
            int num = array.Length;
            checked
            {
                if (array[num - 5] != 48)
                {
                    return false;
                }
                fixed (byte* ptr = array)
                {
                    int num2 = *(unchecked((int*)ptr) + num / 4 - 1);
                    if (num2 != CRLFCRLF)
                    {
                        return false;
                    }
                }
                return true;
            }
        }


        internal void AddToCallbacksList(Action<object> action, object obj)
        {
            SendHandlesCallBackInfo item = new SendHandlesCallBackInfo
            {
                CallBack = action,
                State = obj
            };
            SendHandlesCallbacks.Add(item);
        }




        public override IAsyncResult BeginRead(byte[] array, int num, int num2, AsyncCallback asyncCallback, object obj)
        {
            throw new NotSupportedException();
        }

        public override IAsyncResult BeginWrite(byte[] array, int srcOffset, int num, AsyncCallback asyncCallback, object obj)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("OwinResponseStream");
            }
            if (_closed)
            {
                throw new IOException("Stream has been closed");
            }
            IsBeginWrite = (true);
            byte[] array2 = CreateHttpHandle();
            IList<WriteParam> list = new List<WriteParam>();
            if (array2 != null)
            {
                list.Add(new WriteParam
                {
                    Buffer = array2,
                    Offset = 0,
                    Length = array2.Length
                });
            }
            if (_appDataIsChunked)
            {
                _chunkEndTagWrited = HaveChunkEndTag(array);
                list.Add(new WriteParam
                {
                    Buffer = array,
                    Offset = srcOffset,
                    Length = num
                });
                return ((OwinStream)_base).Write(list, asyncCallback, obj);
            }
            if (_contentSizeSetted)
            {
                list.Add(new WriteParam
                {
                    Buffer = array,
                    Offset = srcOffset,
                    Length = num
                });
                return ((OwinStream)_base).Write(list, asyncCallback, obj);
            }
            byte[] array3 = ByteUtil.IntToByte(num);

            int num2 = array3.Length + num + 2;
            byte[] array4 = new byte[num2];
            Buffer.BlockCopy(array3, 0, array4, 0, array3.Length);
            Buffer.BlockCopy(array, srcOffset, array4, array3.Length, num);
            array4[num2 - 2] = 13;
            array4[num2 - 1] = 10;
            list.Add(new WriteParam
            {
                Buffer = array4,
                Offset = 0,
                Length = num2
            });
            return ((OwinStream)_base).Write(list, asyncCallback, obj);
        }

        private byte[] CreateHttpHandle()
        {
            if (_handleWrited || _closed)
            {
                return null;
            }
            _handleWrited = true;
            foreach (SendHandlesCallBackInfo current in SendHandlesCallbacks)
            {
                current.CallBack(current.State);
            }
            StringBuilder stringBuilder = new StringBuilder();
            int num = 200;
            string text = "OK";
            if (_owinEnv.ContainsKey("owin.ResponseStatusCode"))
            {
                num = (int)_owinEnv["owin.ResponseStatusCode"];
                text = HttpCodeUtil.Get(num);
            }
            if (_owinEnv.ContainsKey("owin.ResponseReasonPhrase"))
            {
                text = (_owinEnv["owin.ResponseReasonPhrase"] as string);
            }
            IDictionary<string, string[]> dictionary = _owinEnv["owin.ResponseHeaders"] as IDictionary<string, string[]>;
            if (dictionary != null && dictionary.Count >= 1)
            {
                foreach (KeyValuePair<string, string[]> current2 in dictionary)
                {
                    if (!current2.Key.Equals("Server", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current2.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        {
                            _contentSizeSetted = true;
                        }
                        if (current2.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && current2.Value != null && current2.Value.Length > 0 && current2.Value[0].Equals("chunked", StringComparison.OrdinalIgnoreCase))
                        {
                            _appDataIsChunked = true;
                        }
                        if (current2.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] value = current2.Value;
                            if (value == null || value.Length < 1 || value[0].Equals("close", StringComparison.OrdinalIgnoreCase))
                            {
                                ConnectionClosed = (true);
                            }
                        }
                        else if (current2.Value != null && current2.Value.Length >= 1)
                        {
                            string[] value2 = current2.Value;
                            for (int i = 0; i < value2.Length; i++)
                            {
                                string arg = value2[i];
                                stringBuilder.AppendFormat("{0}: {1}\r\n", current2.Key, arg);
                            }
                        }
                    }
                }
            }
            stringBuilder.AppendFormat("Server: {0}\r\n", ServerHendleValue);
            stringBuilder.AppendFormat("X-OwinHost-By: {0}\r\n", "OwinDog");
            if (!_appDataIsChunked && !_contentSizeSetted)
            {
                stringBuilder.Append("Transfer-Encoding: chunked\r\n");
                ((IDictionary<string, string[]>)_owinEnv["owin.ResponseHeaders"]).Add("Transfer-Encoding", new string[]
                {
                    "chunked"
                });
            }
            if (ConnectionClosed)
            {
                stringBuilder.AppendFormat("Connection: close\r\n", new object[0]);
            }
            stringBuilder.Append("\r\n");
            string arg2 = string.Format(CultureInfo.InvariantCulture, "HTTP/1.1 {0} {1}\r\n", new object[]
            {
                num.ToString(),
                text
            });
            string s = arg2 + stringBuilder;
            return Encoding.Default.GetBytes(s);
        }

        /// <summary>
        /// 关闭读取Response
        /// </summary>
        public void ResponseEnd()
        {
            if (_rspEndCalled || _closed)
            {
                return;
            }
            _rspEndCalled = true;
            if (!HaveResponse())
            {
                return;
            }
            if (!_contentSizeSetted)
            {
                if (!_chunkEndTagWrited)
                {
                    _chunkEndTagWrited = true;
                    byte[] array = CreateHttpHandle();
                    byte[] array2 = ByteUtil.GetEndByts();
                    if (array == null)
                    {
                        _base.Write(array2, 0, array2.Length);
                        return;
                    }
                    byte[] array3 = new byte[checked(array.Length + array2.Length)];
                    Buffer.BlockCopy(array, 0, array3, 0, array.Length);
                    Buffer.BlockCopy(array2, 0, array3, array.Length, array2.Length);
                    _base.Write(array3, 0, array3.Length);
                }
                return;
            }
            byte[] array4 = CreateHttpHandle();
            if (array4 == null)
            {
                return;
            }
            _base.Write(array4, 0, array4.Length);
        }

        public override void Close()
        {
            if (_closed)
            {
                return;
            }
            try
            {
                ResponseEnd();
            }
            catch
            {
                //ignore
            }
            _owinEnv = null;
            _closed = true;
            _base.Close();
        }
        public new void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (disposing)
            {
                Close();
            }
            _base.Dispose();
            _base = null;
            _closed = true;
            base.Dispose(disposing);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _base.EndWrite(asyncResult);
        }

        ~OwinResponseStream()
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
                return false;
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
                return _base.CanTimeout;
            }
        }


        public override bool CanWrite
        {
            get
            {
                return !_closed && !_disposed && _base.CanWrite;
            }

        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }


        public override long Position { get; set; }


        public override int Read(byte[] array, int num, int num2)
        {
            throw new NotSupportedException();
        }

        public override int ReadByte()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long num, SeekOrigin seekOrigin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long num)
        {
            throw new NotSupportedException();
        }



        public override int WriteTimeout
        {
            get
            {
                return _base.WriteTimeout;
            }
            set
            {
                _base.WriteTimeout = value;
            }

        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            EndWrite(BeginWrite(buffer, offset, count, null, null));
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
            try
            {
                BeginWrite(buffer, offset, count, new AsyncCallback(AsyncCallback), taskCompletionSource);
            }
            catch (Exception exception)
            {
                taskCompletionSource.SetException(exception);
            }
            return taskCompletionSource.Task;
        }

        private void AsyncCallback(IAsyncResult asyncResult)
        {
            TaskCompletionSource<int> taskCompletionSource = asyncResult.AsyncState as TaskCompletionSource<int>;
            if (taskCompletionSource == null)
            {
                return;
            }
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)asyncResult;
            if (customeAsyncResult.SocketIsErrOrClose)
            {
                taskCompletionSource.SetException(new Exception("socket error."));
                return;
            }
            if (customeAsyncResult.SocketIsTimeOut)
            {
                taskCompletionSource.SetException(new Exception("write timeout."));
                return;
            }
            taskCompletionSource.SetResult(1);
        }

        public override void WriteByte(byte b)
        {
            byte[] buffer = new byte[]
            {
                b
            };
            Write(buffer, 0, 1);
        }



        private class SendHandlesCallBackInfo
        {
            public Action<object> CallBack;

            public object State;
        }
    }



}


