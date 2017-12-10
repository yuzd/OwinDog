using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace WebSocket.Demo
{


    #region <WebSocket委托定义>

    // 异步接受客户端连接（握手）的方法代理
    using WebSocketAccept =
        Action<IDictionary<string, object>,     //Accept字典，可以为null
        Func<                                  //握手成功后的回调函数
            IDictionary<string, object>,       //包含 SendAsync, ReceiveAsync, CloseAsync 等关键字的字典
            Task>                              //返回给服务器的表示本回调函数是否执行完成的字典
        >;


    // 异步关闭连的函数代理
    using WebSocketCloseAsync =
    Func<int,                   //关闭的状码代码
        string,                 //说明
        CancellationToken,      //任务是否取消
        Task                    //代表本操作是否完成的任务
    >;

    // 异步读取数据的函数代理
    using WebSocketReceiveAsync =
        Func<ArraySegment<byte>, // 接受数据的缓冲区
            CancellationToken,   // 传递操作是否取消
            Task<                // 返回值
                Tuple<
                    int,      // 第一分量，表示接收到的数据类型（1表示本文数据，2表示二进制数据，8表示对方关闭连接）
                    bool,     // 第二分量，表示是否是一个数据帖的最后一块或者独立块
                    int       // 第三分量，表示有效数据的长度
                >
            >
        >;


    // 异步发送数据的函数代表
    using WebSocketSendAsync =
        Func<ArraySegment<byte>,     // 待发送的缓冲区
            int,                     // 数据类型，只能是1、2、8
            bool,                    // 这一块数据是否是一条信息的最后一块
            CancellationToken,       // 取消任务的通知
            Task                     // 返回值
        >;


    #endregion



    /// <summary>
    /// WebSocket对象
    /// </summary>
    public sealed class WebSocket
    {

        /****************************************************************
         * 这是一个对websocket进行了一定程序封装的对象，
         * 已经有一定的实用价值，使用者可以根据自己的需求进一步完善
         * 包括4个方法和三个代理（你可以改为事件）
         * ==========================================================
         * * 公开的4个方法分别是:
         * Accept:     接受远端WebSocket连接
         * StartRead： 开始接收数据
         * Send：      发送文本数据
         * Close：     关闭与完端的连接
         * -----------------------------------------------------------
         * 3个委托是：
         * OnSend：    表示数据发送完成
         * OnRead：    表示数据读取完成
         * OnClose：   表示远端主动提供断开连接
         **************************************************************/


        #region <共用委托定义>

        public delegate void DelegateReadComplete(object sender, string message);

        public delegate void DelegateWriteComplete(object sender);

        public delegate void DelegateCloseComplete(object sender);

        #endregion



        #region <私有变量>

        /// <summary>
        /// 进行连接的函数
        /// </summary>
        private WebSocketAccept _accept;


        /// <summary>
        /// 发送数据的函数
        /// </summary>
        private WebSocketSendAsync _sendAsync;

        /// <summary>
        /// 接收数据的函数
        /// </summary>
        private WebSocketReceiveAsync _receiveAsync;

        /// <summary>
        /// 关闭连接的函数
        /// </summary>
        private WebSocketCloseAsync _closeAsync;


        /// <summary>
        /// 是否已经关闭
        /// </summary>
        private bool _isClosed = true;

        /// <summary>
        /// 是否已经开始读取循环
        /// </summary>
        private int _reading;

        /// <summary>
        /// 用于保存前边收到的，还不完整的数据
        /// </summary>
        private byte[] _lastReadData;



        #endregion



        #region <共有字段>

        /// <summary>
        /// 数据读取完成
        /// </summary>
        public DelegateReadComplete OnRead;

        /// <summary>
        /// 连接已经断开
        /// </summary>
        public DelegateCloseComplete OnClose;

        /// <summary>
        /// 数据发送完成
        /// </summary>
        public DelegateWriteComplete OnSend;

        #endregion



        #region <共用属性>

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        public string RemoteIpAddress { get; private set; }

        /// <summary>
        /// 客户端端口
        /// </summary>
        public int RemotePort { get; private set; }

        /// <summary>
        /// 本地IP地址
        /// </summary>
        public string LocalIpAddress { get; private set; }

        /// <summary>
        /// 本地端口
        /// </summary>
        public int LocalPort { get; private set; }


        /// <summary>
        /// 请求的路径
        /// </summary>
        public string RequestPath { get; private set; }


        /// <summary>
        /// URL查询字串
        /// </summary>
        public string Query { get; private set; }

        /// <summary>
        /// 是否属于WebSocket连接
        /// </summary>
        public bool IsWebSocket { get; private set; }


        #endregion



        #region <构造与析构>


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owinEnv"></param>
        public WebSocket(IDictionary<string, object> owinEnv)
        {
            // 获取Accept方法
            _accept = owinEnv.Get<WebSocketAccept>("websocket.Accept");
            if (_accept == null) return;

            IsWebSocket = true;

            // SERVER
            RemoteIpAddress = owinEnv.Get<string>("server.RemoteIpAddress");
            RemotePort = int.Parse(owinEnv.Get<string>("server.RemotePort"));
            LocalIpAddress = owinEnv.Get<string>("server.LocalIpAddress");
            LocalPort = int.Parse(owinEnv.Get<string>("server.LocalPort"));
            //var islocal = owinEnv.Get<bool>("server.IsLocal");

            // OWIN
            RequestPath = owinEnv.Get<string>("owin.RequestPath");
            Query = owinEnv.Get<string>("owin.RequestQueryString");
            // owinEnv.Get<string>("owin.RequestMethod");  GET/POST/....

        }

        #endregion



        #region <接受连接与关闭连接的操作>


        /// <summary>
        /// 响应客户端握手请求
        /// </summary>
        /// <returns>返回真表示按WebSocket的方式成功连接</returns>
        public bool Accept()
        {
            if (_accept == null) return false;

            //执行连接操作
            _accept(null, sckEnv =>
            {
                //从字典中取出服务器提供的WebSocket操作函数
                _sendAsync = sckEnv.Get<WebSocketSendAsync>("websocket.SendAsync");
                _receiveAsync = sckEnv.Get<WebSocketReceiveAsync>("websocket.ReceiveAsync");
                _closeAsync = sckEnv.Get<WebSocketCloseAsync>("websocket.CloseAsync");

                //标记连接成功
                _isClosed = false;

                //通知服务器（容器），表示连接事件已经处理完成
                return Task.Delay(0);
            });

            //表示是websocket消息并同意握手
            return true;

        }


        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            if (_isClosed) return;
            _closeAsync(1000, null, CancellationToken.None).ContinueWith(t => { if (t.IsFaulted) { var x = t.Exception; } }); ;
            _isClosed = true;
        }


        #endregion



        #region <发送操作>


        /// <summary>
        /// 发送以字节数组表示的文本内容
        /// <para>强调：必须是UTF8编码的文本数据</para>
        /// </summary>
        /// <param name="bytMessage">UTF8文本的字节数据</param>
        public void Send(byte[] bytMessage)
        {
            if (bytMessage == null || bytMessage.Length < 1) throw new ArgumentNullException();
            if (_isClosed) throw new Exception();

            var t = _sendAsync(new ArraySegment<byte>(bytMessage), 1, true, CancellationToken.None);
            t.ContinueWith(InternalWriteComplete);

        }


        /// <summary>
        /// 发送文本
        /// </summary>
        /// <param name="message">UTF8文本内容</param>
        public void Send(string message)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentNullException();
            if (_isClosed) throw new Exception();

            Send(Encoding.UTF8.GetBytes(message));
        }

        /// <summary>
        /// 发送完成的回调函数
        /// </summary>
        /// <param name="task"></param>
        private void InternalWriteComplete(Task task)
        {

            var err = task.IsFaulted || task.IsCanceled;
            if (err)
            {
                _isClosed = true;
                if (task.IsFaulted) { var tmp = task.Exception; }

                if (OnClose != null)
                {
                    OnClose(this);
                    OnClose = null;
                }

                return;
            }

            if (OnSend == null) return;

            OnSend(this);
        }


        #endregion



        #region <接收操作>


        /// <summary>
        /// 开始接收数据（无阻塞）
        /// </summary>
        public void StartRead()
        {
            if (_isClosed) throw new Exception();
            if (Interlocked.CompareExchange(ref _reading, 1, 0) == 1) return;

            InternalRealRead(new byte[8129]); //接收缓冲区不要低于4K字节
        }

        private void InternalRealRead(byte[] buffer)
        {
            if (_isClosed) return;
            var arraySeg = new ArraySegment<byte>(buffer);
            var task = _receiveAsync(arraySeg, CancellationToken.None);
            task.ContinueWith(_t =>
            {
                var err = _t.IsCanceled || _t.IsFaulted;
                if (_t.IsFaulted) { var tmp = _t.Exception; }
                InternalReadComplte(arraySeg, _t.Result.Item1, _t.Result.Item2, _t.Result.Item3, err);
            });
        }


        /// <summary>
        /// 内部调用的，用于数据接受成功的回调函数
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="type"></param>
        /// <param name="endOfMessage"></param>
        /// <param name="size"></param>
        /// <param name="error"></param>
        private void InternalReadComplte(ArraySegment<byte> buffer, int type, bool endOfMessage, int size, bool error)
        {

            //只接受文本数据，否则关闭
            if (type == 8 || type == 2 || error)
            {
                _isClosed = true;
                _closeAsync(1000, null, CancellationToken.None);
                if (OnClose != null) { OnClose(this); OnClose = null; }
                return;
            }

            // 如果一帧数据已经完成
            if (endOfMessage)
            {
                var lastSize = _lastReadData == null ? 0 : _lastReadData.Length;
                var data = new byte[size + lastSize];
                if (lastSize != 0) Buffer.BlockCopy(_lastReadData, 0, data, 0, lastSize);
                Buffer.BlockCopy(buffer.Array, 0, data, lastSize, size);
                _lastReadData = null;

                if (OnRead != null)
                {
                    var s = Encoding.UTF8.GetString(data);
                    if (OnRead != null) OnRead(this, s);
                }

                //继续接收
                InternalRealRead(buffer.Array);
                return;
            }


            //不完整数据帧,保存起来
            var oldSize = _lastReadData == null ? 0 : _lastReadData.Length;
            var tmpData = new byte[oldSize + size];
            if (oldSize > 0) Buffer.BlockCopy(_lastReadData, 0, tmpData, 0, oldSize);
            Buffer.BlockCopy(buffer.Array, 0, tmpData, oldSize, size);
            _lastReadData = tmpData;

            //继续接收
            InternalRealRead(buffer.Array);

        }

        #endregion


    }


    /// <summary>
    /// Dictionary扩展类
    /// </summary>
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// 获取指定的键值
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="dictionary">当前字典</param>
        /// <param name="key">键</param>
        /// <returns></returns>
        internal static T Get<T>(this IDictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary.TryGetValue(key, out value) ? (T)value : default(T);
        }
    }



}
