using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Service
{
   
    internal static class ActionStoreManage
    {
        private static readonly ActionStore _actionStore = new ActionStore();

        /// <summary>
        /// 添加action
        /// </summary>
        /// <param name="action"></param>
        /// <returns>所在的分组index</returns>
        public static int Add(Action action)
        {
            return _actionStore.AddAction(action, 30);
        }

        /// <summary>
        /// 根据所在的分组index 去删除包含的action
        /// </summary>
        /// <param name="num">所在分组的index</param>
        /// <param name="action"></param>
        public static void Remove(int num, Action action)
        {
            _actionStore.RemoveAction(num, action);
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="elapsedEventArgs"></param>
        public static void Excute(object obj, ElapsedEventArgs elapsedEventArgs)
        {
            IList<Action> list = _actionStore.Get();
            if (list == null || list.Count < 1)
            {
                return;
            }
            foreach (Action current in list)
            {
                current();
            }
        }


        private class ActionStore
        {
            /// <summary>
            /// 共有120组
            /// </summary>
            private const int MaxSize = 120;

            /// <summary>
            /// 游标
            /// </summary>
            private int _index;

            private readonly object lockObject = new object();

            private readonly List<Action>[] ActionList = new List<Action>[MaxSize];
            public ActionStore()
            {
                //初始化
                for (int i = 0; i < ActionList.Length; i++)
                {   
                    ActionList[i] = new List<Action>();
                }
            }

            /// <summary>
            /// 获取组action 然后清空该组 且 游标自增
            /// </summary>
            /// <returns></returns>
            public IList<Action> Get()
            {

                IList<Action> result;
                lock (lockObject)
                {
                    if (ActionList[_index].Count < 1)
                    {
                        _index = (_index + 1) % MaxSize;//这种写法的好处是自增最大不会超过MaxSize
                        result = null;
                    }
                    else
                    {
                        //去除分组下的所有的action集合
                        IList<Action> list = ActionList[_index];
                        //清空
                        ActionList[_index] = new List<Action>();
                        _index = (_index + 1) % MaxSize;
                        //返回
                        result = list;
                    }
                }
                return result;
            }

            /// <summary>
            /// 分组 添加action 
            /// </summary>
            /// <param name="item"></param>
            /// <param name="num"></param>
            /// <returns></returns>
            public int AddAction(Action item, int num)
            {
                int result;
                lock (lockObject)
                {
                    int num2 = (_index + num) % MaxSize;
                    ActionList[num2].Add(item);
                    result = num2;
                }
                return result;
            }

            /// <summary>
            /// 移除所在分组的action
            /// </summary>
            /// <param name="num"></param>
            /// <param name="item"></param>
            public void RemoveAction(int num, Action item)
            {
                if (num < 0 || num >= MaxSize)
                {
                    return;
                }
                lock (lockObject)
                {
                    if (ActionList[num].Contains(item))
                    {
                        ActionList[num].Remove(item);
                    }
                }
            }
        }
    }
    
}
