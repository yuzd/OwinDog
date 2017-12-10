using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Service;

namespace OwinEngine
{
  
    public class OwinAdapter
    {


        private readonly string _htmlRootBinPath;

        private readonly string _appRootPath;

        /// <summary>
        /// Owin入口
        /// </summary>
        public Func<IDictionary<string, object>, Task> OwinMain { get; set; }

        /// <summary>
        /// Owin Adapter Class
        /// </summary>
        public object OwinMainClass { get; set; }



        public OwinAdapter()
        {
            _htmlRootBinPath = Path.Combine(ApplicationInfo.Wwwroot, "bin");
            if (!Directory.Exists(_htmlRootBinPath))
            {
                _htmlRootBinPath = Path.Combine(ApplicationInfo.Wwwroot, "Bin");
                if (!Directory.Exists(_htmlRootBinPath))
                {
                    _htmlRootBinPath = "";
                }
            }
            _appRootPath = ApplicationInfo.Approot;
            if (!Directory.Exists(_appRootPath))
            {
                _appRootPath = "";
            }
        }


        /// <summary>
        /// 遍历每个dll 去找到指定的class
        /// </summary>
        /// <param name="libPath">bin目录</param>
        /// <returns></returns>
        private bool FindFromDirectory(string libPath)
        {
            FileInfo[] files = new DirectoryInfo(libPath).GetFiles("*.dll", SearchOption.AllDirectories);
            if (files.Length < 1)
            {
                return false;
            }

            foreach (var fileInfo in files)
            {
                Assembly assembly = null;
                try
                {
                    assembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(fileInfo.FullName));
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (FindFromAssembly(assembly))
                    {
                        return true;
                    }
                }
                catch
                {
                   continue;
                }
            }
            return false;
        }

        /// <summary>
        /// 从程序集里面找到 OwinMain 方法的Class
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private bool FindFromAssembly(Assembly assembly)
        {
            Type[] types = null;
            try
            {
                types = assembly.GetTypes();
            }
            catch 
            {
                //Console.WriteLine(e);
                return false;
            }
            if (types.Length < 1)
            {
                return false;
            }

            foreach (var type in types)
            {
                MethodInfo methodInfo = null;
                try
                {
                    methodInfo = type.GetMethod("OwinMain", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    continue;
                }
                if (methodInfo == null || methodInfo.ReturnType != typeof(Task))
                {
                    continue;
                }
                ParameterInfo[] parameters = methodInfo.GetParameters();
                if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(IDictionary<string, object>)))
                {
                    try
                    {
                        OwinMainClass = Activator.CreateInstance(type);
                    }
                    catch (Exception value)
                    {
                        Console.WriteLine("*** AdapterLoader: An exception occurred when the class was created.");
                        Console.WriteLine("--> File:  {0}", assembly.Location);
                        Console.WriteLine("--> Class: {0}", type.Name);
                        Console.WriteLine(value);
                        continue;
                    }
                    Delegate @delegate = Delegate.CreateDelegate(typeof(Func<IDictionary<string, object>, Task>), OwinMainClass, methodInfo);
                    OwinMain = ((Func<IDictionary<string, object>, Task>)@delegate);
                    return true;
                }
            }
            return false;
        }

        public bool AdapterLoaderSuccess()
        {
            return (!string.IsNullOrEmpty(_appRootPath) || !string.IsNullOrEmpty(_htmlRootBinPath))
                && ((!string.IsNullOrEmpty(_htmlRootBinPath)
                && FindFromDirectory(_htmlRootBinPath)) || (!string.IsNullOrEmpty(_appRootPath)
                && FindFromDirectory(_appRootPath)));
        }

    }
}
