using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Service;
using Util;

namespace OwinEngine
{
    public sealed class OwinWebSocketServer
    {
        private Stream _owinRepStream;//OwinStream

        private Stream _owinReqStream;//OwinStream

        private Action _callback;

        private string _protocol = "";

        private readonly string _origin = "null";

        private TaskCompletionSource<int> _taskCompletionSource;

        private readonly byte[] _buff = new byte[8192];

        private readonly WebSocketReciveDataParse _parser;

        private bool _IsEof;

        private readonly Queue<ReciveWapper> _reciveWapperQueue = new Queue<ReciveWapper>();

        private readonly string _webSocketKey = "";
        public Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>> Accept { get; private set; }

      
        public OwinWebSocketServer(Stream owinReqStream, Stream owinRepStream, IDictionary<string, string[]> environment, Action action)
        {
            //客户端请求的Sec-WebSocket-Key 是随机的 服务端要回应 才能握手成功
            _webSocketKey = (environment.Keys.Contains("Sec-WebSocket-Key") ? environment["Sec-WebSocket-Key"].FirstOrDefault<string>() : "");
            if (string.IsNullOrEmpty(_webSocketKey))
            {
                throw new Exception("WebSocket: Sec-WebSocket-Key Is Null.");
            }
            _webSocketKey = _webSocketKey.Trim();
            _origin = (environment.Keys.Contains("Origin") ? environment["Origin"].FirstOrDefault<string>() : "");
            _owinRepStream = owinRepStream;
            _owinReqStream = owinReqStream;
            _callback = action;
            //握手回应
            Accept = (new Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>(AcceptFunc));
            _parser = new WebSocketReciveDataParse();
        }

        private void AcceptFunc(IDictionary<string, object> dictionary, Func<IDictionary<string, object>, Task> func)
        {
            _taskCompletionSource = new TaskCompletionSource<int>();
            object obj;
            if (dictionary != null && dictionary.TryGetValue("websocket.SubProtocol", out obj))
            {
                _protocol = (obj as string);
            }
            SHA1CryptoServiceProvider sHA1CryptoServiceProvider = new SHA1CryptoServiceProvider();
            byte[] bytes = Encoding.ASCII.GetBytes(_webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
            byte[] inArray = sHA1CryptoServiceProvider.ComputeHash(bytes);
            string responseKey = Convert.ToBase64String(inArray);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("HTTP/1.1 101 Switching Protocols\r\n");
            stringBuilder.Append("Connection: Upgrade\r\n");
            stringBuilder.Append("Upgrade: WebSocket\r\n");
            stringBuilder.Append("Server: OwinDog/1.0\r\n");
            stringBuilder.AppendFormat("WebSocket-Origin: {0}\r\n", _origin);
            stringBuilder.AppendFormat("Date: {0}\r\n", DateTime.Now.ToUniversalTime().ToString("r"));
            stringBuilder.AppendFormat("Sec-WebSocket-Accept: {0}\r\n\r\n", responseKey);
            byte[] handsBytes = Encoding.ASCII.GetBytes(stringBuilder.ToString());
            try
            {
                //服务端回应握手信息
                _owinRepStream.Write(handsBytes, 0, handsBytes.Length);
            }
            catch (Exception exception)
            {
                _taskCompletionSource.SetException(exception);
                throw;
            }
            Dictionary<string, object> websocketFuncs = new Dictionary<string, object>();
            websocketFuncs.Add("websocket.Version", "1.0");
            websocketFuncs.Add("websocket.SendAsync", new Func<ArraySegment<byte>, int, bool, CancellationToken, Task>(SendAsync));
            websocketFuncs.Add("websocket.ReceiveAsync", new Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>>(ReceiveAsync));
            websocketFuncs.Add("websocket.CloseAsync", new Func<int, string, CancellationToken, Task>(CloseAsync));
            websocketFuncs.Add("websocket.CallCancelled", CancellationToken.None);
            websocketFuncs.Add("websocket.Protocol", _protocol);
            Task task = null;
            try
            {
                task = func(websocketFuncs);//握手成功  回发给Adapter return Task.Delay(0);
            }
            catch (Exception exception2)
            {
                _taskCompletionSource.SetException(exception2);
                throw;
            }
            if (task == null)
            {
                return;
            }
            if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
            {
                OnOpenClientCalled(task);
                return;
            }
            task.ContinueWith(new Action<Task>(OnOpenClientCalled));
        }


        public bool IsCompleted()
        {
            return _taskCompletionSource != null && _taskCompletionSource.Task.Wait(3000) && _taskCompletionSource.Task.IsCompleted;
        }

      
        /// <summary>
        /// 处理 onopen 的调用 没有发送数据就无视
        /// </summary>
        /// <param name="task"></param>
        private void OnOpenClientCalled(Task task)
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (task.IsFaulted)
                {
                    if (task.Exception != null) _taskCompletionSource.SetException(task.Exception);
                }
                if (task.IsCanceled)
                {
                    _taskCompletionSource.SetCanceled();
                }
                return;
            }
            _taskCompletionSource.SetResult(1);
            _owinReqStream.BeginRead(_buff, 0, _buff.Length, new AsyncCallback(Recvive), null);
        }

        /// <summary>
        /// //异步读取回调处理方法
        /// </summary>
        /// <param name="state"></param>
        private void Recvive(object state)
        {
            if (_IsEof)
            {
                return;
            }
            int reciveLength = _owinReqStream.EndRead((IAsyncResult)state);
            if (reciveLength < 1)
            {
                _IsEof = true;
            }
            if (reciveLength > 0)
            {
                _parser.Parse(_buff, 0, reciveLength);
            }
            _IsEof = _parser.IsEof;
            ParseReciveWapper();
            if (_IsEof)
            {
                try
                {
                    CloseConnection(_parser.abool ? 1009 : 1000, null, CancellationToken.None, _parser.abool);
                }
                catch
                {
                    //ignore
                }
                return;
            }
            //再次执行异步读取操作
            _owinReqStream.BeginRead(_buff, 0, _buff.Length, new AsyncCallback(Recvive), null);
        }

        private void WriteCallBack(IAsyncResult asyncResult)
        {
            _owinRepStream.EndWrite(asyncResult);
            CustomeAsyncResult customeAsyncResult = (CustomeAsyncResult)asyncResult;
            bool flag = customeAsyncResult.SocketIsTimeOut || customeAsyncResult.SocketIsErrOrClose;
            AsyncState asyncState = (AsyncState)asyncResult.AsyncState;
            if (asyncState.CancellationToken != CancellationToken.None && asyncState.CancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (flag)
            {
                asyncState.TaskCompletionSource.SetException(new IOException());
                return;
            }
            asyncState.TaskCompletionSource.SetResult(0);
        }

        

      
        private Task<Tuple<int, bool, int>> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            TaskCompletionSource<Tuple<int, bool, int>> taskCompletionSource = new TaskCompletionSource<Tuple<int, bool, int>>();
            if (_IsEof)
            {
                taskCompletionSource.SetException(new Exception());
                return taskCompletionSource.Task;
            }
            ReciveWapper item = new ReciveWapper
            {
                Buffer = buffer,
                CancellationToken = cancellationToken,
                TaskCompletionSource = taskCompletionSource
            };
            lock (_reciveWapperQueue)
            {
                _reciveWapperQueue.Enqueue(item);
            }
            ParseReciveWapper();//返回给Adapte 类型 长度 和 byte数组
            return taskCompletionSource.Task;
        }

        private Task CloseAsync(int status, string description, CancellationToken cancellationToken)
        {
            if (_IsEof)
            {
                return Task.FromResult<int>(0);
            }
            return CloseConnection(status, description, cancellationToken, true);
        }

        private Task SendAsync(ArraySegment<byte> buffer, int messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
            if (_IsEof)
            {
                taskCompletionSource.SetException(new Exception());
                return taskCompletionSource.Task;
            }
            int num2 = 2;
            byte b;
            if (buffer.Count < 126)
            {
                b = (byte)buffer.Count;
            }
            else if (buffer.Count >= 126 && buffer.Count < 65535)
            {
                num2 += 2;
                b = 126;
            }
            else
            {
                num2 += 8;
                b = 127;
            }
            byte[] array = new byte[num2 + buffer.Count];
            array[0] = (byte)((endOfMessage ? 128 : 0) + messageType);
            array[1] = b;
            int dstOffset = 2;
            if (b == 126)
            {
                array[dstOffset++] = (byte)((buffer.Count & 65280) >> 8);
                array[dstOffset++] = (byte)(buffer.Count & 255);
            }
            else if (b == 127)
            {
                taskCompletionSource.SetException(new Exception("Data is too long..."));
                return taskCompletionSource.Task;
            }
            Buffer.BlockCopy(buffer.Array, buffer.Offset, array, dstOffset, buffer.Count);
            _owinRepStream.BeginWrite(array, 0, array.Length, new AsyncCallback(WriteCallBack), new AsyncState
            {
                TaskCompletionSource = taskCompletionSource,
                CancellationToken = cancellationToken
            });
            return taskCompletionSource.Task;
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status">1000 通常終了 
        /// 1001表示端点“离开” 例如服务器关闭或浏览器导航到其他页面 
        /// 1002表示端点因为协议错误而终止连接 
        /// 1003表示端点由于它收到了不能接收的数据类型 1009表示端点因接收到的消息对它的处理来说太大而终止连接</param>
        /// <param name="description"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        private Task CloseConnection(int status, string description, CancellationToken cancellationToken, bool flag)
        {
            _IsEof = true;
            if (status < 1000)
            {
                status = 1000;
            }
            byte[] desc = null;
            if (!string.IsNullOrEmpty(description))
            {
                desc = Encoding.UTF8.GetBytes(description);
            }
            byte[] array2 = new byte[4 + ((desc == null) ? 0 : desc.Length)];
            array2[0] = 136;
            array2[1] = (byte) (2 + (byte)((desc == null) ? 0 : desc.Length));
            array2[2] = (byte)((status & 65280) >> 8);
            array2[3] = (byte)(status & 255);
            if (desc != null)
            {
                Buffer.BlockCopy(desc, 0, array2, 4, desc.Length);
            }

            if (_owinRepStream != null)
            {
                _owinRepStream.Write(array2, 0, array2.Length);
            }
            if (!flag)
            {
                return Task.Delay(0, cancellationToken);
            }
            Task task = new Task(new Action(dispose));
            task.Start();
            return task;
            //Task.Delay(3000, cancellationToken).ContinueWith(new Action<Task>(dispose), cancellationToken);
            //return Task.FromResult<int>(0);
        }

        /// <summary>
        /// Parse 完成一个 处理一个
        /// </summary>
        private void ParseReciveWapper()
        {
            lock (_reciveWapperQueue)
            {
                while (_reciveWapperQueue.Count > 0)
                {
                    WebSocketReciveDataParse.ReciveData reciveData = _parser.GetReciveData();
                    ReciveWapper reciveWapper = _reciveWapperQueue.Dequeue();
                    if (!(reciveWapper.CancellationToken != CancellationToken.None) || !reciveWapper.CancellationToken.IsCancellationRequested)
                    {
                        if (reciveData == null && _IsEof)
                        {
                            //读完了但没有数据
                            reciveWapper.TaskCompletionSource.SetException(new SocketException(10061));
                        }
                        else if (reciveData == null && !_IsEof)
                        {
                            //没有数据 但还没有读完 正在处理中
                            if (_reciveWapperQueue.Count < 1)
                            {
                                _reciveWapperQueue.Enqueue(reciveWapper);//放回去
                                break;
                            }
                            ReciveWapper[] array = _reciveWapperQueue.ToArray();
                            _reciveWapperQueue.Clear();
                            _reciveWapperQueue.Enqueue(reciveWapper);// 让它先出
                            for (int i = 0; i < array.Length; i++)
                            {
                                ReciveWapper item = array[i];
                                _reciveWapperQueue.Enqueue(item);
                            }
                            break;
                        }
                        else if (reciveData != null && reciveWapper.Buffer.Count < reciveData.BytesLength)
                        {
                            reciveWapper.TaskCompletionSource.SetException(new Exception("buffer buffer too small."));
                        }
                        else
                        {
                            if (reciveData != null)
                            {
                                Buffer.BlockCopy(reciveData.Data, 0, reciveWapper.Buffer.Array, reciveWapper.Buffer.Offset, reciveData.BytesLength);
                                reciveWapper.TaskCompletionSource.SetResult(new Tuple<int, bool, int>(reciveData.Type, reciveData.IsEof, reciveData.BytesLength));
                            }
                        }
                    }
                }
            }
        }

        private void dispose()
        {
            Thread.Sleep(2000);
            if (_callback != null)
            {
                _callback();
            }
            _callback = null;
            _owinReqStream = null;
            _owinRepStream = null;
        }

        private sealed class AsyncState
        {
            public TaskCompletionSource<int> TaskCompletionSource;

            public CancellationToken CancellationToken;
        }

        private class ReciveWapper
        {
            public ArraySegment<byte> Buffer;

            public CancellationToken CancellationToken;

            public TaskCompletionSource<Tuple<int, bool, int>> TaskCompletionSource;
        }
    }
}
