using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Model;
using OwinEngine;

namespace Service
{
    public class UvLoopThread
    {
        private readonly LibUv _libuv;

        private readonly LoopHandle _loopHandle;

        private readonly AsyncHandle _asyncHand1;

        private readonly AsyncHandle _asyncHand2;

        private readonly Action<OwinSocket> _owinHttpProcess;

        private Queue<QueueUserAction> UserPostActionQueue = new Queue<QueueUserAction>();

        private Queue<QueueUserAction> UserPostActionQueueBak = new Queue<QueueUserAction>();

        /// <summary>
        /// Bind the pipe to a file path (Unix) or a name (Windows).
        /// </summary>
        private readonly string _name = string.Empty;

        private readonly Thread _loopThread;

        private UvPipeHandle _uvPipeHandle;

        private LibUv.BufferStruct _bufferStruct;

        private int _disposeFlag;

        private readonly object lockObject = new object();

        private int _postFlag;


        public UvLoopThread(LibUv libUv, string name, Action<OwinSocket> owinHttpProcess)
        {
            if (libUv == null)
            {
                throw new ArgumentNullException("libuv");
            }
            _libuv = libUv;
            _name = name;
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
                _loopThread.Start(taskCompletionSource);
                taskCompletionSource.Task.Wait();
            }
            catch
            {
                return null;
            }
            taskCompletionSource = new TaskCompletionSource<int>();
            AsyncSendUserPostAction(new Action<object>(PostListenHandleToLoop), taskCompletionSource);
            return taskCompletionSource.Task;
        }

        private void PostListenHandleToLoop(object tcsObj)
        {
            TaskCompletionSource<int> taskCompletionSource = (TaskCompletionSource<int>)tcsObj;
            if (string.IsNullOrEmpty(_name))
            {
                taskCompletionSource.SetResult(0);
                return;
            }
            IntPtr intPtr = Marshal.AllocHGlobal(4);
            _bufferStruct = _libuv.CreateBufferStruct(intPtr, 4);
            _uvPipeHandle = new UvPipeHandle();
            
            _uvPipeHandle.Init(_loopHandle, true);
            UvPipeStream uvPipeStream = new UvPipeStream();
            uvPipeStream.Init(_loopHandle);
            //链接
            uvPipeStream.PipeConnect(_uvPipeHandle, _name, new Action<UvPipeStream, int, Exception, object>(ConnectionCallBack), taskCompletionSource);
        }

        private void StartUvWalk(object obj)
        {
            _libuv.Walk(_loopHandle, new LibUv.Walk_Callback(Walk_Callback), IntPtr.Zero);
        }

        public void AsyncSendUserPostAction(Action<object> action, object obj)
        {
            if (Thread.CurrentThread.ManagedThreadId == _loopHandle.LoopRunThreadId)
            {
                action(obj);
                return;
            }

            lock (lockObject)
            {
                UserPostActionQueue.Enqueue(new QueueUserAction
                {
                    CallbackAction = action,
                    State = obj
                });
                _postFlag++;
                _postFlag %= 2;
            }
            if (_postFlag == 0)
            {
                _asyncHand1.AsyncSend();
                return;
            }
            _asyncHand2.AsyncSend();
        }

        private void UserPostActionExcute()
        {
            lock (lockObject)
            {
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

        private LibUv.BufferStruct AllocCallback(UvStreamHandle uvStreamHandle, int num, object obj)
        {
            return _bufferStruct;
        }

        /// <summary>
        /// 接收并处理请求
        /// </summary>
        /// <param name="serverHandle"></param>
        /// <param name="status"></param>
        /// <param name="error"></param>
        /// <param name="state"></param>
        private void Accept(UvStreamHandle serverHandle, int status, Exception error, object state)
        {
            if (error != null || status != 0)
            {
                return;
            }
            ListenHandle listenHandle = new ListenHandle();
            listenHandle.Init(_loopHandle, new Action<Action<object>, object>(AsyncSendUserPostAction));
            listenHandle.TcpNodealy(true);
            serverHandle.Accept(listenHandle);
            OwinSocket owinSocket = null;
            try
            {
                owinSocket = new OwinSocket(listenHandle, new Action<Action<object>, object>(AsyncSendUserPostAction));
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
                    listenHandle.Close();
                }
            }
        }

        private void ConnectionCallBack(UvPipeStream uvPipeStream, int num, Exception ex, object obj)
        {
            var taskCompletionSource = (TaskCompletionSource<int>) obj;
            uvPipeStream.Dispose();
            if (ex != null)
            {
                taskCompletionSource.SetException(ex);
                return;
            }
            try
            {
                //当tcp 的 http 请求被重定向到这里的时候会回调 ReadCallback 方法
                _uvPipeHandle.Read(AllocCallback, ReadCallback, null);
                taskCompletionSource.SetResult(0);
            }
            catch (Exception ex2)
            {
                Console.WriteLine(ex2);
                _uvPipeHandle.Dispose();
                taskCompletionSource.SetException(ex2);
            }
        }

        /// <summary>
        /// 接收被重定向到pipe handle的请求并处理
        /// </summary>
        /// <param name="uvStreamHandle"></param>
        /// <param name="num"></param>
        /// <param name="ex"></param>
        /// <param name="obj"></param>
        private void ReadCallback(UvStreamHandle uvStreamHandle, int num, Exception ex, object obj)
        {
            if (num < 0)
            {
                _uvPipeHandle.Dispose();
                return;
            }
            if (_uvPipeHandle.PipePendingCount() == 0)
            {
                return;
            }
            Accept(_uvPipeHandle, 0, null, null);
        }

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

     

        private void InitLoopThread(object obj)
        {
            _loopHandle.Init(_libuv);
            _asyncHand1.Init(_loopHandle, new Action(UserPostActionExcute));
            _asyncHand2.Init(_loopHandle, new Action(UserPostActionExcute));
            TaskCompletionSource<int> taskCompletionSource = (TaskCompletionSource<int>)obj;
            taskCompletionSource.SetResult(0);
            while (true)
            {
                bool flag = false;
                try
                {
                    _loopHandle.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
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
            if (Interlocked.CompareExchange(ref _disposeFlag, 0, -1) == 1)
            {
                return;
            }
            _asyncHand1.UvRef();
            _asyncHand2.UvRef();
            _libuv.Walk(_loopHandle, new LibUv.Walk_Callback(MainThreadWalkCallBack), IntPtr.Zero);
            _loopHandle.Start();
            _loopHandle.Dispose();
        }
        private void AllHandleDisPose(object obj)
        {
            if (_uvPipeHandle != null && !_uvPipeHandle.IsInvalid && !_uvPipeHandle.IsClosed)
            {
                _uvPipeHandle.Dispose();
                _uvPipeHandle = null;
            }
            _asyncHand1.UvUnRef();
            _asyncHand2.UvUnRef();
        }

        private void LoopHandleStop(object obj)
        {
            _loopHandle.Stop();
        }




       

    }
   
}
