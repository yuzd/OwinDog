namespace Owin.AspEngine
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Hosting;

    public static class AspNet
    {
        private static AppFunc _appFunc;
        private static AspApplicationHost _appHost;
        private static bool _aspunloading = false;
        private static readonly AspRequestBroker _broker;
        private static int _ids = 10;
        private static object _lckCreateHost = new object();
        private static readonly string _pPath = AppDomain.CurrentDomain.GetData(".appPath").ToString();
        private static ConcurrentDictionary<int, ReqInfo> _tasks = new ConcurrentDictionary<int, ReqInfo>();

        static AspNet()
        {
            //工作路径
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            //执行代理
            _broker = new AspRequestBroker(new AspRequestBroker.DelegateRead(AspNet.Read), new AspRequestBroker.DelegateWrite(AspNet.Write), new AspRequestBroker.DelegateWriteHeader(AspNet.WriteHeader), new AspRequestBroker.DelegateWriteHttpStatus(AspNet.WriteStatus), new AspRequestBroker.DelegateRequestEnd(AspNet.RequestEnd), new AspRequestBroker.DelegateDomainUnload(AspNet.AspUnload));
        }

        /// <summary>
        /// 卸载
        /// </summary>
        private static void AspUnload()
        {
            _aspunloading = true;
            Console.WriteLine(" * Asp Applicaton unload.....");
            _appHost = null;
            _appFunc = null;
            if (_tasks.Count > 0)
            {
                foreach (ReqInfo info in _tasks.Values)
                {
                    info.RequestWorkTask.SetCanceled();
                }
                _tasks.Clear();
            }
            _aspunloading = false;
        }

        private static void CreateAspApplicationHost()
        {
            //创建并配置用于承载 ASP.NET 的应用程序域。
            _appHost = (AspApplicationHost) ApplicationHost.CreateApplicationHost(typeof(AspApplicationHost), "/", _pPath);
            _appHost.SetRequestBroker(_broker);
            //请求执行委托
            _appFunc = new AppFunc(_appHost.Process);
        }

        private static int CreateId()
        {
            int num = Interlocked.Increment(ref _ids);
            if (num > 0x7fffd8ef) //int32最大数的一半
            {
                Interlocked.Exchange(ref _ids, 10);
            }
            return num;
        }

        public static Task Process(IDictionary<string, object> env)
        {
            if (_aspunloading)
            {
                return Task.FromResult<bool>(true);
            }
            if (_appHost == null)
            {
                lock (_lckCreateHost)
                {
                    if (_appHost == null)
                    {
                        CreateAspApplicationHost();
                    }
                }
            }
            if (_appHost == null)
            {
                return null;
            }
            int reqid = CreateId();
            ReqInfo info = new ReqInfo(env);
            AspRequestData data = new AspRequestData(reqid, env);
            _tasks[reqid] = info;
            RealProcess(data);
            return info.RequestWorkTask.Task;
        }

        private static void RealProcess(AspRequestData data)
        {
            try
            {
                _appFunc(data);//真正的处理request请求
            }
            catch (Exception)
            {
                RequestEnd(data.RequestId, false);
            }
        }

        private static void RequestEnd(int id, bool iskeep)
        {
            ReqInfo info = null;
            if (_tasks.TryRemove(id, out info))
            {
                if (iskeep)
                {
                    info.RequestWorkTask.SetResult(true);
                }
                else
                {
                    info.RequestWorkTask.SetCanceled();
                }
            }
        }


        /// <summary>
        /// 往input 流 读入
        /// </summary>
        /// <param name="id"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static int Read(int id, byte[] buffer, int offset, int size)
        {
            ReqInfo info = _tasks[id];
            return info.ReadStream.Read(buffer, offset, size);
        }

        /// <summary>
        /// 往out 流 写入
        /// </summary>
        /// <param name="id"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        private static void Write(int id, byte[] buffer, int offset, int size)
        {
            ReqInfo info = _tasks[id];
            info.WriteStream.Write(buffer, offset, size);
        }

        /// <summary>
        /// 写Response Header
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        private static void WriteHeader(int id, string name, string value)
        {
            ReqInfo info = _tasks[id];
            info.ResponseHeader[name] = new string[] { value };
        }

        /// <summary>
        /// 写状态和描述
        /// </summary>
        /// <param name="id"></param>
        /// <param name="statusCode"></param>
        /// <param name="statusDesc"></param>
        private static void WriteStatus(int id, int statusCode, string statusDesc)
        {
            _tasks[id].WriteStauts(statusCode, statusDesc);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AppFunc(AspRequestData data);

        private class ReqInfo
        {
            /// <summary>
            /// Owin环境变量
            /// </summary>
            private IDictionary<string, object> _env;

            public ReqInfo(IDictionary<string, object> env)
            {
                this._env = env;
                this.RequestWorkTask = new TaskCompletionSource<bool>();
                this.ReadStream = env["owin.RequestBody"] as Stream;
                this.WriteStream = env["owin.ResponseBody"] as Stream;
                this.ResponseHeader = env["owin.ResponseHeaders"] as IDictionary<string, string[]>;
            }

            /// <summary>
            /// 返回码和描述
            /// </summary>
            /// <param name="code"></param>
            /// <param name="desc"></param>
            public void WriteStauts(int code, string desc)
            {
                this._env["owin.ResponseStatusCode"] = code;
                if (!string.IsNullOrEmpty(desc))
                {
                    this._env["owin.ResponseReasonPhrase"] = desc;
                }
            }

            /// <summary>
            /// 读的流
            /// </summary>
            public Stream ReadStream { get; private set; }

            /// <summary>
            /// 手动控制任务工作流  使用 TaskCompletionSource 类将基于事件的异步操作转换为 Task
            /// </summary>
            public TaskCompletionSource<bool> RequestWorkTask { get; private set; }

            /// <summary>
            /// 返回头
            /// </summary>

            public IDictionary<string, string[]> ResponseHeader { get; private set; }

            /// <summary>
            /// 写的流
            /// </summary>
            public Stream WriteStream { get; private set; }
        }
    }
}

