using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Model;
using OwinEngine;
using Util;

namespace Service
{
    public sealed class Listener
    {
        private readonly LibUv _libuv;

        private readonly LoopHandle _loopHandle;

        private readonly AsyncHandle _asyncHand1;

        private readonly AsyncHandle _asyncHand2;

        private ListenHandle _listenHandle;

        /// <summary>
        /// 绑定地址
        /// </summary>
        private readonly string _bindAddr;

        /// <summary>
        /// 绑定端口
        /// </summary>
        private readonly int _bindPort;

        /// <summary>
        /// pipe绑定路径
        /// </summary>
        private readonly string _tmpPath = string.Empty;

        private readonly Action<OwinSocket> _owinHttpProcess;

        #region 异步执行的消息队列
        private Queue<QueueUserAction> UserPostActionQueue = new Queue<QueueUserAction>();

        private Queue<QueueUserAction> UserPostActionQueueBak = new Queue<QueueUserAction>(); 
        #endregion

        /// <summary>
        /// 当前主线程的线程ID
        /// </summary>
        private static readonly int _currentProcessId;

        private readonly Thread _loopThread;

        private UvPipeHandle _uvPipeHandle;

        private readonly List<UvPipeHandle> _pipeHandleList = new List<UvPipeHandle>();

        private readonly object lockObject = new object();


        private static readonly int _uvPipeHandleOffset;

        private int _connectCount;

        private int _disposeFlag;

        private int _postFlag;
        static Listener()
        {
            _currentProcessId = Process.GetCurrentProcess().Id;
            _uvPipeHandleOffset = (SystemUtil.IsWindowOs() ? ((IntPtr.Size == 4) ? 256 : 480) : 0);
        }

     
        private void LoopHandleStop(object obj)
        {
            _loopHandle.Stop();
        }
        public Listener(LibUv libUv, string bindAddr, int bindPort, string tmpPath, Action<OwinSocket> owinHttpProcess)
        {
            if (libUv == null)
            {
                throw new ArgumentNullException("libuv");
            }
            _libuv = libUv;
            _bindAddr = bindAddr;//绑定的ip
            _bindPort = bindPort;//绑定的端口
            _tmpPath = tmpPath;
            _owinHttpProcess = owinHttpProcess;
            _loopHandle = new LoopHandle();
            _asyncHand1 = new AsyncHandle();
            _asyncHand2 = new AsyncHandle();
            _loopThread = new Thread(InitLoopThread);
        }


        public Task Start()
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
            try
            {
                _loopThread.Start(taskCompletionSource);//启动线程
                taskCompletionSource.Task.Wait();//等待上面的线程返回
            }
            catch
            {
                return null;
            }
            taskCompletionSource = new TaskCompletionSource<int>();
            //采用libuv的 asyncHandle消息机制 调用PostListenHandleToLoop 初始化 tcp监听 
            AsyncSendUserPostAction(new Action<object>(PostListenHandleToLoop), taskCompletionSource);
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// 监听Tcp请求
        /// </summary>
        /// <param name="tcsObj"></param>
        private void PostListenHandleToLoop(object tcsObj)
        {
            TaskCompletionSource<int> taskCompletionSource = (TaskCompletionSource<int>)tcsObj;
            try
            {
                _listenHandle = new ListenHandle();
                _listenHandle.Init(_loopHandle, null);
                _listenHandle.TcpNodealy(true);
                _listenHandle.TcpBind(_bindAddr, _bindPort);
                _listenHandle.Listen(1000, new Action<UvStreamHandle, int, Exception, object>(OnNewConnectionCallback), null);
            }
            catch (Exception exception)
            {
                taskCompletionSource.SetException(exception);
                return;
            }
            if (!string.IsNullOrEmpty(_tmpPath))
            {
                _uvPipeHandle = new UvPipeHandle();
                _uvPipeHandle.Init(_loopHandle, false);
                _uvPipeHandle.PipeBind(_tmpPath);
                _uvPipeHandle.Listen(100, new Action<UvStreamHandle, int, Exception, object>(PipeConnectionCallBack), null);
                taskCompletionSource.SetResult(0);
                return;
            }
            taskCompletionSource.SetResult(0);
        }

        private void StartUvWalk(object obj)
        {
            _libuv.Walk(_loopHandle, new LibUv.Walk_Callback(Walk_Callback), IntPtr.Zero);
        }

        /// <summary>
        /// 做pipe的重定向
        /// </summary>
        /// <param name="uvStreamHandle"></param>
        /// <param name="index"></param>
        private void WriteHandleFree(UvStreamHandle uvStreamHandle, int index)
        {
            UvPipeHandle uvPipeHandle = _pipeHandleList[index];
            WriteHandle writeHandle = new WriteHandle();
            writeHandle.Init(_loopHandle);
            writeHandle.Free(uvPipeHandle, uvStreamHandle, Write2_CallBack, uvStreamHandle);
        }


        /// <summary>
        /// 当发送异步信号后 会调用async的callback 会执行队列里面的方法
        /// </summary>
        /// <param name="action">callback方法</param>
        /// <param name="obj">callback的参数</param>
        public void AsyncSendUserPostAction(Action<object> action, object obj)
        {
            if (Thread.CurrentThread.ManagedThreadId == _loopHandle.LoopRunThreadId)//Listener
            {
                action(obj);
                return;
            }
            lock (lockObject)//因为是多线程可以同时发送唤醒消息
            {
                //使用队列的方式 先存进队列 然后在 UserPostActionExcute 里面 POP出来执行
                UserPostActionQueue.Enqueue(new QueueUserAction
                {
                    CallbackAction = action,
                    State = obj
                });
                _postFlag++;
                _postFlag %= 2;
            }
            //_asyncHand1 和 _asyncHand2 交替 提高性能
            if (_postFlag == 0)
            {
                _asyncHand1.AsyncSend();
                return;
            }
            _asyncHand2.AsyncSend();
        }

        /// <summary>
        /// 执行消息队里里面的action
        /// </summary>
        private void UserPostActionExcute()
        {
            lock (lockObject)
            {
                //互换 消息管道
                Queue<QueueUserAction> queue = UserPostActionQueue;
                UserPostActionQueue = UserPostActionQueueBak;
                UserPostActionQueueBak = queue;
                //执行完所有的消息
                while (queue.Count > 0)
                {
                    QueueUserAction queueUserAction = queue.Dequeue();
                    try
                    {
                        queueUserAction.CallbackAction(queueUserAction.State);
                    }
                    catch
                    {
                        //ignore
                    }
                }
            }
        }



        private void Walk_Callback(IntPtr intPtr, IntPtr intPtr2)
        {
            if (intPtr == IntPtr.Zero)
            {
                return;
            }
            HandleBase handleBase = HandleBase.GetObjectFromHandel<HandleBase>(intPtr);
            if (!handleBase.IsInvalid && !handleBase.IsClosed && handleBase != _asyncHand1 && handleBase != _asyncHand2)
            {
                handleBase.Dispose();
            }
        }

        private void MainThreadWalkCallBack(IntPtr intPtr, IntPtr intPtr2)
        {
            if (intPtr == IntPtr.Zero)
            {
                return;
            }
            HandleBase handleBase = HandleBase.GetObjectFromHandel<HandleBase>(intPtr);
            try
            {
                if (!handleBase.IsInvalid && !handleBase.IsClosed && handleBase != _asyncHand1 && handleBase != _asyncHand2)
                {
                    handleBase.Dispose();
                }
            }
            catch (Exception source)
            {
                ExceptionDispatchInfo.Capture(source);
            }
        }

        private static void Write2_CallBack(int num, Exception ex, object obj)
        {
            ((UvStreamHandle)obj).Dispose();
        }

        /// <summary>
        /// 管道连接
        /// </summary>
        /// <param name="uvStreamHandle"></param>
        /// <param name="num"></param>
        /// <param name="ex"></param>
        /// <param name="obj"></param>
        private unsafe void PipeConnectionCallBack(UvStreamHandle uvStreamHandle, int num, Exception ex, object obj)
        {
            if (num < 0)
            {
                return;
            }
            UvPipeHandle uvPipeHandle = new UvPipeHandle();
            uvPipeHandle.Init(_loopHandle, true);
            try
            {
                uvStreamHandle.Accept(uvPipeHandle);
            }
            catch (Exception)
            {
                uvPipeHandle.Dispose();
                return;
            }
            if (_uvPipeHandleOffset > 0) // 是windows系统
            {
                *(int*)((void*)(uvPipeHandle.InternalGetHandle() + _uvPipeHandleOffset)) = _currentProcessId;
            }
            _pipeHandleList.Add(uvPipeHandle);
        }

        /// <summary>
        /// 新连接回调事件
        /// </summary>
        /// <param name="serverHandle"></param>
        /// <param name="status"></param>
        /// <param name="error"></param>
        /// <param name="state"></param>
        private void OnNewConnectionCallback(UvStreamHandle serverHandle, int status, Exception error, object state)
        {
            if (error != null || status != 0)
            {
                return;
            }
            ListenHandle clientHandle = new ListenHandle();
            clientHandle.Init(_loopHandle, new Action<Action<object>, object>(AsyncSendUserPostAction));
            clientHandle.TcpNodealy(true);
            serverHandle.Accept(clientHandle);

            //int.MaxValue=2147483647
            _connectCount++;
            if (_connectCount > 2147483547)
            {
                _connectCount = 1;
            }

            int num2 = _connectCount % (_pipeHandleList.Count + 1);
            if (num2 != _pipeHandleList.Count)
            {
                WriteHandleFree(clientHandle, num2);
                return;
            }

            OwinSocket owinSocket = null;
            try
            {
                owinSocket = new OwinSocket(clientHandle, new Action<Action<object>, object>(AsyncSendUserPostAction));
                //主流程开始执行
                _owinHttpProcess(owinSocket);
            }
            catch (Exception)
            {
                if (owinSocket != null)
                {
                    owinSocket.Dispose();
                }
                else
                {
                    clientHandle.Close();
                }
            }
        }

        /// <summary>
        /// 发起卸载监视器
        /// </summary>
        public void DisPose()
        {
            AsyncSendUserPostAction(new Action<object>(AllHandleDisPose), null);
            if (!_loopThread.Join(3000))
            {
                AsyncSendUserPostAction(new Action<object>(StartUvWalk), null);
            }
            if (!_loopThread.Join(3000))
            {
                AsyncSendUserPostAction(LoopHandleStop, null);
            }
            if (!_loopThread.Join(300))
            {
                Interlocked.Exchange(ref _disposeFlag, 1);
                Thread.Sleep(800);
                _loopThread.Abort();
            }
        }



        /// <summary>
        /// 单独的Uv线程
        /// </summary>
        /// <param name="obj"></param>
        private void InitLoopThread(object obj)
        {
            //申请内存 调用uv_loop_init
            _loopHandle.Init(_libuv);

            //先绑定asyncHandle 监视器 后绑定Tcp监视器 
            //AsyncHandle 注册 当执行async_send的时候 唤醒loopHandle 执行callback方法
            _asyncHand1.Init(_loopHandle, UserPostActionExcute);
            _asyncHand2.Init(_loopHandle, UserPostActionExcute);
            TaskCompletionSource<int> taskCompletionSource = (TaskCompletionSource<int>)obj;
            taskCompletionSource.SetResult(0);//结束task 的 wait

            while (true)
            {
                bool flag = false;
                try
                {
                    //因为上面已有async的监视器 有要监听的事件 所以会阻塞
                    //调用uv_run
                    _loopHandle.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("***** libuv.run error: " + ex.Message);
                    if (!(ex is NullReferenceException))
                    {
                        throw ex;
                    }
                    flag = true;
                }
                if (!flag)
                {
                    break;
                }
                Console.WriteLine("===== Try to start again ===");
            }

            //如果已经终止了 就直接退出
            if (Interlocked.CompareExchange(ref _disposeFlag, 0, -1) == 1)
            {
                return;
            }
            _asyncHand1.UvRef();
            _asyncHand1.dispose();
            _asyncHand2.UvRef();
            _asyncHand2.dispose();
            //遍历循环中的handle //所有非内部的handle，调用回调 进行关闭
            _libuv.Walk(_loopHandle, new LibUv.Walk_Callback(MainThreadWalkCallBack), IntPtr.Zero);
            _loopHandle.Start();
            _loopHandle.Dispose();
        }

        /// <summary>
        /// 监视器全部卸载后 loop 结束阻塞
        /// </summary>
        /// <param name="obj"></param>
        private void AllHandleDisPose(object obj)
        {
            if (_listenHandle != null && !_listenHandle.IsClosed && !_listenHandle.IsInvalid)
            {
                //关闭Tcp监视器
                _listenHandle.Dispose();
                _listenHandle = null;
            }
            if (_uvPipeHandle != null && !_uvPipeHandle.IsClosed && !_uvPipeHandle.IsInvalid)
            {
                //关闭Pipe管道监视器
                _uvPipeHandle.Dispose();
                _uvPipeHandle = null;
            }
            foreach (UvPipeHandle current in _pipeHandleList)
            {
                if (current != null && !current.IsClosed && !current.IsInvalid)
                {
                    current.Dispose();
                }
            }

            //关闭异步监视器
            _asyncHand1.UvUnRef();
            _asyncHand2.UvUnRef();
        }





      
    }

    public class QueueUserAction
    {
        public Action<object> CallbackAction;

        public object State;
    }
}
