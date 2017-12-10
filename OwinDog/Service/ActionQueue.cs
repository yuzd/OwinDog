using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Util;

namespace Service
{
    /// <summary>
    /// 每隔指定时间 执行所包含的Action
    /// </summary>
    public static class ActionQueue
    {

        private static bool _flag;

        private static readonly List<ActionParam> _actionParamList;

        public static DateTime Time { get; set; }

        public static long LongTimes { get; set; }

        static ActionQueue()
        {
            _flag = false;
            _actionParamList = new List<ActionParam>();
            initDateTimeAndLong();
            new Thread(new ThreadStart(Init))
            {
                IsBackground = true
            }.Start();
        }

        private static void ExcuteActionParam(object obj)
        {
            ActionParam actionParam = obj as ActionParam;
            try
            {
                if (actionParam != null) actionParam.Excute();
            }
            catch
            { 
                //ignore
            }
            finally
            {
                if (actionParam != null) actionParam.IsBreak = false;
            }
        }

        public static void AddAction(Action action, int times)
        {
            lock (_actionParamList)
            {
                ActionParam item = new ActionParam
                {
                    Excute = action,
                    times = times,
                    longTimes = CommonUtil.CurrentTimes()
                };
                _actionParamList.Add(item);
            }
        }

        private static void Init()
        {
            while (!_flag)
            {
                initDateTimeAndLong();
                Run();
                Thread.Sleep(200);
            }
            _flag = true;
        }

        private static void initDateTimeAndLong()
        {
            Time= DateTime.Now;
            LongTimes = CommonUtil.CurrentTimes();//从1970/01/01 00:00:01 到现在经过的毫秒数了
        }
        private static void Run()
        {
            if (_actionParamList == null || _actionParamList.Count < 1)
            {
                return;
            }
            long num = CommonUtil.CurrentTimes();//当前毫秒数
            lock (_actionParamList)
            {
                foreach (ActionParam current in _actionParamList)
                {
                    if (checked(num - current.times) >= (long)current.longTimes && !current.IsBreak)
                    {
                        current.longTimes = num;
                        current.IsBreak = true;
                        if (!ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(ExcuteActionParam), current))
                        {
                            current.IsBreak = false;
                        }
                    }
                }
            }
        }


        private class ActionParam
        {
            public Action Excute;

            public int times;

            public long longTimes;

            public bool IsBreak;
        }
    }
}

