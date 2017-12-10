using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Service
{
    public class SimpleThreadPool
    {

        private readonly int iCount;
        private readonly int MaxCount;

        private readonly AutoResetEvent AutoResetEvent = new AutoResetEvent(false);//设置为false 无信号 当调用set的时候 线程才会启动执行

        private readonly ConcurrentQueue<Worker> _workerQueue = new ConcurrentQueue<Worker>();

        private long CurrentLongTimes;

        private int bInt;
        public SimpleThreadPool() : this(0)
        {
        }

        public SimpleThreadPool(int num)
        {
            int num2 = Environment.ProcessorCount;
            if (num2 < 1)
            {
                num2 = 1;
            }
            if (num2 > 128)
            {
                num2 = 128;
            }
            if (num < 1)
            {
                num = num2;
            }
            if (num < 2)
            {
                num = 2;
            }
            if (num > 128)
            {
                num = 128;
            }
            iCount = num;

            int num3 = 24 + (int)(Math.Log((double)num2) * 32.0);
            MaxCount = iCount + num3;
            StartNewThread();
            ActionQueue.AddAction(new Action(AutoIncrement), 100);
        }

        public int GetThreadCount()
        {
            return _workerQueue.Count;
        }

        private void AutoIncrement()
        {
            if (_workerQueue == null || _workerQueue.Count < 1)
            {
                return;
            }
            int num = Interlocked.CompareExchange(ref bInt, 0, -1);
            if (num >= MaxCount)//超过了最大线程数量
            {
                return;
            }
            long num2 = ActionQueue.LongTimes;

            if (num < iCount)
            {
                if (num < 1 || num2 - CurrentLongTimes >= 500)
                {
                    StartNewThread();
                }
                return;
            }
            int num3 = (num < iCount + (MaxCount - iCount) / 3) ? 3000 : 8000;
            if (num2 - CurrentLongTimes < num3)
            {
                return;
            }
            StartNewThread();
        }

        public void UnsafeQueueUserWorkItem(Action<object> action, object obj)
        {
            if (action == null)
            {
                throw new Exception("SimpleThreadPool Callback Action is null.");
            }
            _workerQueue.Enqueue(new Worker
            {
                Action = action,
                state = obj
            });
            AutoResetEvent.Set();
        }

        private void StartNewThread()
        {
            //bInt == -1 ? bInt=0:bInt
            if (Interlocked.CompareExchange(ref bInt, 0, -1) >= MaxCount)
            {
                return;
            }
            try
            {
                new Thread(new ThreadStart(b))
                {
                    IsBackground = true
                }.Start();
            }
            catch
            {
                return;
            }
            CurrentLongTimes = ActionQueue.LongTimes;
        }

        private void b()
        {
            int num = Interlocked.Increment(ref bInt);
            if (num <= MaxCount)
            {
                try
                {
                    C();
                }
                catch
                {
                }
            }
            Interlocked.Decrement(ref bInt);
        }

        private void C()
        {
            long num = 0;
            while (true)
            {
                Worker worker = null;
                if (!_workerQueue.TryDequeue(out worker))
                {
                    worker = null;
                }
                if (worker == null)
                {
                    int num2 = Interlocked.CompareExchange(ref bInt, 0, -1);
                    if (num2 > 1)
                    {
                        if (num == 0)
                        {
                            num = ActionQueue.LongTimes;
                        }
                        else if ((ActionQueue.LongTimes - num) > 5000)//当没有action要执行 线程隔5秒自动销毁
                        {
                            break;
                        }
                    }
                    AutoResetEvent.WaitOne(1000);// 等待一秒
                }
                else
                {
                    num = 0;
                    try
                    {
                        worker.Action(worker.state);
                    }
                    catch
                    {
                        //ignore
                    }
                }
            }
        }



        public class Worker
        {
            public Action<object> Action;

            public object state;
        }
    }
}
