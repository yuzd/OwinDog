using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Model;
using OwinEngine;
using Service;
using Util;

namespace Dog
{
    class Program
    {
        #region private


        private static LibUv libUv;

        private static Listener _listener;


        internal static Dictionary<string, int> CmdOptions = new Dictionary<string, int>(9)
                                {
                                    {
                                        "-v",
                                        0
                                    },
                                    {
                                        "-V",
                                        1
                                    },
                                    {
                                        "-h",
                                        2
                                    },
                                    {
                                        "-p",
                                        3
                                    },
                                    {
                                        "-port",
                                        4
                                    },
                                    {
                                        "-r",
                                        5
                                    },
                                    {
                                        "-root",
                                        6
                                    },
                                    {
                                        "-addr",
                                        7
                                    },
                                    {
                                        "-ipaddr",
                                        8
                                    }
                                };
        #endregion

        public static void Main(string[] args)
        {
            int port = 8088;//默认端口
            string ip = "0.0.0.0";//本机
            string root = null;

            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string text3 = args[i];
                    string key;
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (text3[0] == '-' && (key = text3) != null)
                    {
                        int number;
                        if (CmdOptions.TryGetValue(key, out number))
                        {
                            switch (number)
                            {
                                case 0:
                                case 1:
                                    PrintVersion();
                                    return;
                                case 2:
                                    GetHelp();
                                    return;
                                case 3:
                                case 4:
                                    if (i < args.Length - 1)
                                    {
                                        string s = args[++i];
                                        port = int.Parse(s);
                                    }
                                    break;
                                case 5:
                                case 6:
                                    if (i < args.Length - 1)
                                    {
                                        root = args[++i].Trim(new char[] { '\'' }).Trim(new char[] { '"' }).Trim();
                                    }
                                    break;
                                case 7:
                                case 8:
                                    if (i < args.Length - 1)
                                    {
                                        ip = args[++i];
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            string fullPath = Path.GetFullPath(GetApplicationLocalPath(args));
            ApplicationInfo.SetApplicationPath(fullPath, root);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);
            TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>(UnobservedTaskException);
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolve);
            AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(AssemblyLoad);
            if (!LoadLibuv())
            {
                return;
            }
            RealMain(ip, port);
        }

        /// <summary>
        /// 获取执行目录
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        private static string GetApplicationLocalPath(string[] array)
        {
            bool isWindowOs = SystemUtil.IsWindowOs();
            string environmentVariable = "";

            if (array != null && array.Length > 0)
            {
                //查看参数是否带有 --ｈｏｓｔｒｏｏｔ
                environmentVariable = array.FirstOrDefault(WithHostRoot);
                if (!string.IsNullOrEmpty(environmentVariable))
                {
                    environmentVariable = environmentVariable.Substring(environmentVariable.IndexOf('=') + 1);
                }
            }
            if (string.IsNullOrEmpty(environmentVariable))
            {
                //获取环境变量的HOST_ROOT_PATH的地址
                environmentVariable = Environment.GetEnvironmentVariable("HOST_ROOT_PATH", EnvironmentVariableTarget.Process);
                if (!string.IsNullOrEmpty(environmentVariable))
                {
                    Environment.SetEnvironmentVariable("HOST_ROOT_PATH", null, EnvironmentVariableTarget.Process);
                }
            }
            if (string.IsNullOrEmpty(environmentVariable))
            {
                //获取当前exe执行路径
                environmentVariable = GetExePath();
                if (!string.IsNullOrEmpty(environmentVariable) && !Directory.Exists(Path.Combine(environmentVariable, "runtime", "native")))
                {
                    environmentVariable = null;
                }
            }
            if (string.IsNullOrEmpty(environmentVariable))
            {
                //获取当前程序集目录
                environmentVariable = AppDomain.CurrentDomain.BaseDirectory;
            }

            //参数校验
            environmentVariable = ((environmentVariable == null) ? null : environmentVariable.Trim(new char[]
            {
                '\'',
                '"'
            }));
            if (string.IsNullOrEmpty(environmentVariable))
            {
                throw new Exception("OwinDog directory path is null.");
            }
            var path = environmentVariable;
            if (!Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            if (isWindowOs ? (path.Length > 1 & path[1] == ':') : (path[0] == '/'))
            {
                return path;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }
        /// <summary>
        /// //可获得当前执行的exe的文件名
        /// </summary>
        /// <returns></returns>
        private static string GetExePath()
        {
            return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        }


        private static bool WithHostRoot(string text)
        {
            return text.StartsWith("--hostroot=");
        }

        private static void RealMain(string ip, int port)
        {
            if (port < 1)
            {
                port = 8088;
            }
            AppDomain.CurrentDomain.SetData(".appPath", ApplicationInfo.Wwwroot);
            AppDomain.CurrentDomain.SetData(".webServer", "OwinDog");
            OwinAdapter owinAdapter = new OwinAdapter();
            //加载 宿主的 Adapter
            if (!owinAdapter.AdapterLoaderSuccess())
            {
                Console.WriteLine("* Load Owin Adapter Or Owin Application Failed.");
                AppDomain.CurrentDomain.SetData(".appPath", null);
            }
            else
            {
                ApplicationInfo.OwinAdapter = (owinAdapter);
            }
            //输出当前计算机的CPU内核数
            int num2 = Environment.ProcessorCount >> 1;
            if (num2 < 1)
            {
                num2 = 1;
            }
            if (num2 > 16)
            {
                num2 = 16;
            }
            string tempPath = string.Empty;
            if (num2 > 1)
            {
                tempPath = (SystemUtil.IsWindowOs() ? "\\\\.\\pipe\\owindog_" : "/tmp/owindog_") + Guid.NewGuid().ToString("n");
            }

            #region Listener

            _listener = new Listener(libUv, ip, port, tempPath, new Action<OwinSocket>(OwinHttpWorkerManage.OwinHttpProcess));
            Task task = _listener.Start();
            if (task == null)
            {
                Console.WriteLine("* Create libuv Listener Failed ...");
                return;
            }
            try
            {
                task.Wait();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (AggregateException ex)
            {
                ex.Handle((Exception e) => true);
            }

            if (task.IsFaulted || task.IsCanceled)
            {
                AggregateException exception = task.Exception;
                Console.WriteLine("------------------------------------");
                Console.WriteLine("   *** Create libuv Listener Failed ***   ");
                Console.WriteLine("------------------------------------");
                Console.WriteLine(exception);
                _listener.DisPose();
                return;
            }
            #endregion

            #region UvLoopThread

            List<UvLoopThread> list = new List<UvLoopThread>();
            if (!string.IsNullOrEmpty(tempPath))
            {
                //如果是2核 那么启动1个 如果3个启动2个 以此类推
                for (int i = 1; i < num2; i++)
                {
                    UvLoopThread uvLoopThread = new UvLoopThread(libUv, tempPath, new Action<OwinSocket>(OwinHttpWorkerManage.OwinHttpProcess));
                    Task task2 = uvLoopThread.Start();
                    if (task2 == null)
                    {
                        Console.WriteLine("*** A UvLoop Thread Not Startted.");
                    }
                    else
                    {
                        task2.Wait();
                        if (task2.IsFaulted || task2.IsCanceled)
                        {
                            if (task2.IsFaulted)
                            {
                                if (task2.Exception != null) Console.WriteLine(task2.Exception.ToString());
                            }
                            uvLoopThread.DisPose();
                        }
                        else
                        {
                            list.Add(uvLoopThread);
                        }
                    }
                }
            }
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Elapsed += new ElapsedEventHandler(ActionStoreManage.Excute);
            timer.Start();
            Console.WriteLine("==================================================================");
            Console.WriteLine(" OwinDog Web Server Startup  ... ... OK.");
            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine(" OwinDog Version: {0}, Start time: {1:yyy-MM-dd HH:mm:ss}", ApplicationInfo.Version, DateTime.Now);
            Console.WriteLine(" Port: {0}, IP Address: {1}", port, ip);
            Console.WriteLine(" Root: {0}.", ApplicationInfo.Wwwroot);
            Console.WriteLine(" Enter 'q' Or 'quit' Or 'exit' To Exit.");
            Console.WriteLine("==================================================================");
            Console.WriteLine();
            HodeOn();
            Console.WriteLine("* Stopping, Please wait ...");
            Console.WriteLine();
            timer.Stop();
            timer.Close();
            timer.Dispose();
            _listener.DisPose();
            foreach (UvLoopThread current in list)
            {
                current.DisPose();
            }
            Thread.Sleep(500);
            #endregion

        }

        /// <summary>
        /// 未捕获的异常处理
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="unhandledExceptionEventArgs"></param>
        private static void UnhandledException(object obj, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            string newLine = Environment.NewLine;
            Exception ex = (Exception)unhandledExceptionEventArgs.ExceptionObject;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("==================== OwinDog Error Message ========================={0}", newLine);
            stringBuilder.AppendFormat("Sender: {0}, Sender TypeName: {1}" + newLine, obj, obj.GetType().Name);
            stringBuilder.AppendFormat("Exception Source: {0}, TargetSite Name: {1}" + newLine, ex.Source, ex.TargetSite.Name);
            stringBuilder.AppendFormat("Message is:{1}{0}{1}", ex.Message, newLine);
            stringBuilder.AppendFormat("StackTrace is:{1}{0}{1}", ex.StackTrace, newLine);
            stringBuilder.AppendFormat("IsTerminating: {0}" + newLine, unhandledExceptionEventArgs.IsTerminating);
            stringBuilder.AppendFormat(newLine, new object[0]);
            Console.WriteLine(stringBuilder.ToString());
        }

        /// <summary>
        /// AppDomain.CurrentDomain.Load 的回调事件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assemblyLoadEventArgs"></param>
        private static void AssemblyLoad(object obj, AssemblyLoadEventArgs assemblyLoadEventArgs)
        {
            if (assemblyLoadEventArgs.LoadedAssembly.IsDynamic)
            {
                return;
            }
            AssemblyUtils.AddAssembly(assemblyLoadEventArgs.LoadedAssembly);
        }

        /// <summary>
        /// 未Catch到的异常
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="unobservedTaskExceptionEventArgs"></param>
        private static void UnobservedTaskException(object obj, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            if (!unobservedTaskExceptionEventArgs.Observed)
            {
                unobservedTaskExceptionEventArgs.SetObserved();
            }
        }

        /// <summary>
        /// 随着项目规模的逐渐扩大，项目引用的dll也越来越多，这些dll默认情况下全部都需要放在跟主程序相同的目录下，
        /// dll一多，主程序的目录就会显得非常凌乱。那么有没有什么办法可以把dll放到其他目录下也能正确加载呢，答案是肯定的，就是利用AppDomain的AssemblyResolve事件。
        /// AssemblyResolve事件在.Net对程序集的解析失败时触发，返回一个Assembly对象。因此，我们只要在这个事件的处理程序里手动加载对应目录的dll，
        /// 并把对应dll的Assembly对象返回，.Net就能正确加载对应的dll了。
        /// 使用AssemblyResolve事件除了本文介绍的，能在任意目录加载程序集外，应该还可以从其他特殊的地方加载程序集
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="resolveEventArgs"></param>
        /// <returns></returns>
        private static Assembly AssemblyResolve(object obj, ResolveEventArgs resolveEventArgs)
        {
            AppDomain appDomain = (AppDomain)obj;
            string text = appDomain.ApplyPolicy(resolveEventArgs.Name);
            Assembly assembly = null;
            try
            {
                if (text != resolveEventArgs.Name)
                {
                    assembly = Assembly.Load(text);
                }
                else
                {
                    assembly = AssemblyUtils.AassemblyName(new AssemblyName(text));
                }
            }
            catch
            {
                assembly = null;
            }
            if (assembly == null && text.Split(new char[]
            {
                ','
            })[0].LastIndexOf(".resources", StringComparison.Ordinal) < 0)
            {
                Console.WriteLine("*** Load Assembly Failed: {0}", resolveEventArgs.Name);
            }
            return assembly;
        }



        /// <summary>
        /// 加载LibUv程序集
        /// </summary>
        /// <returns></returns>
        private static bool LoadLibuv()
        {
            libUv = new LibUv();
            string libuvPath = Path.Combine(ApplicationInfo.AppPtah, "runtime", "native", "libuv");
            if (libUv.IsWindows)
            {
                libuvPath = Path.Combine(libuvPath, "win", (IntPtr.Size == 4) ? "x32" : "x64", "libuv.dll");
            }
            else if (libUv.IsDarwin)//MacOSX 
            {
                libuvPath = Path.Combine(libuvPath, "oth", "libuv.dylib");
            }
            else
            {
                libuvPath = Path.Combine(libuvPath, "lin", (IntPtr.Size == 4) ? "x32" : "x64", "libuv.so.1");
            }
            if (!File.Exists(libuvPath))
            {
                Console.WriteLine("*** Error: Can not found:{0}", libuvPath);
                return false;
            }
            try
            {
                libUv.Initialization(libuvPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("*** Error: Load libuv  failed! ***");
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }


        /// <summary>
        /// 程序挂起
        /// </summary>
        private static void HodeOn()
        {
            while (true)
            {
                Console.Write(">>> ");
                string text = null;
                while (text == null)
                {
                    text = Console.ReadLine();
                    if (text == null)
                    {
                        Thread.Sleep(3000);
                    }
                }
                text = text.Trim();

                string a;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if ((a = text.ToLower()) != null)
                {
                    if (a == "quit" || a == "exit" || a == "q")
                    {
                        return;
                    }
                    if (a == "help" || a == "h")
                    {
                        GetHelp();
                        continue;
                    }
                    if (a == "version" || a == "v")
                    {
                        PrintVersion();
                        continue;
                    }
                }
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 打印版本信息
        /// </summary>
        private static void PrintVersion()
        {
            Console.WriteLine("OwinDog Http Server");
            Console.WriteLine("Version: {0}", ApplicationInfo.Version);
        }
        private static void GetHelp()
        {
            Console.WriteLine("OwinDog 启动参数：");
            Console.WriteLine("        -p 指定端口号，如 owindog -p 80");
            Console.WriteLine("        （不加该参数时默认端口号是8088）");
            Console.WriteLine();
            Console.WriteLine(@"        -root 网站或webapi的物理路径，如 owindog -root d:\myapi\wwwroot。");
            Console.WriteLine(@"        （不加该参数时，owindog.exe所在文件夹下的site内的wwwroot目录）");
            Console.WriteLine();
            Console.WriteLine("另注：");
            Console.WriteLine("        1，你可以使用不同的批处理文件，设置不同的端口和路径，开启多个WEB应用。");
            Console.WriteLine("        2，路径上如果有空格，请用双引号把路径括起来。");
            Console.WriteLine("        3，本程序不支持中文路径，不要把这个程序放在中文路径中。");
            Console.WriteLine(@"       4，runtime\lib\mono\gac文件夹中存放的是比较常用的公用程序集");
            Console.WriteLine("所支持的操作系统版本：");
            Console.WriteLine("        Windows：64位操作系统 32位操作系统");
            Console.WriteLine("        Linux：64位 CentOS 6.5或Ubuntu 12.04以上版本(注意libc的版本号不能低于 2.12)");
        }
    }
}
