

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Service;

namespace Util
{
    public static class AssemblyUtils
    {
        private static readonly ConcurrentDictionary<string, object> _assemblyObject;

        private static readonly ConcurrentDictionary<string, Assembly> _assemblyConcurrentDictionary;

        private static readonly string[] _assemblyStringArray;
        static AssemblyUtils()
        {
            _assemblyObject = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
            _assemblyConcurrentDictionary = new ConcurrentDictionary<string, Assembly>(StringComparer.Ordinal);
            List<string> list = new List<string>();
            string text = Path.Combine(ApplicationInfo.Wwwroot, "bin");
            if (!Directory.Exists(text))
            {
                text = Path.Combine(new string[]
				{
					ApplicationInfo.Wwwroot + "Bin"
				});
                if (Directory.Exists(text))
                {
                    list.Add(text);
                }
            }
            else
            {
                list.Add(text);
            }
            string text2 = ApplicationInfo.Approot;
            if (!string.IsNullOrEmpty(text2) && Directory.Exists(text2))
            {
                list.Add(text2);
            }
            string text3 = ApplicationInfo.AppPtah;
            if (!string.IsNullOrEmpty(text3))
            {
                text3 = Path.Combine(text3, "lib");
                if (Directory.Exists(text3))
                {
                    list.Add(text3);
                }
            }
            if (list.Count > 0)
            {
                string[] array = list.ToArray();
                string[] array2 = array;
                for (int i = 0; i < array2.Length; i++)
                {
                    string path = array2[i];
                    try
                    {
                        string[] directories = Directory.GetDirectories(path);
                        if (directories.Length > 0)
                        {
                            list.AddRange(directories);
                        }
                    }
                    catch
                    {
                        //ignore
                    }
                }
                _assemblyStringArray = list.ToArray();
            }
        }

        public static Assembly AassemblyName(AssemblyName assemblyName)
        {
            string name = assemblyName.Name;
            Assembly assembly;
            if (_assemblyConcurrentDictionary.TryGetValue(name, out assembly))
            {
                return assembly;
            }
            object orAdd = _assemblyObject.GetOrAdd(name, new object());
            try
            {
                lock (orAdd)
                {
                    if (_assemblyConcurrentDictionary.TryGetValue(name, out assembly))
                    {
                        return assembly;
                    }
                    assembly = AStr(name);
                    if (assembly != null)
                    {
                        _assemblyConcurrentDictionary[name] = assembly;
                    }
                }
            }
            finally
            {
                _assemblyObject.TryRemove(name, out orAdd);
            }
            return assembly;
        }

        public static Assembly AStr(string str)
        {
            if (_assemblyStringArray == null)
            {
                return null;
            }
            string[] a = _assemblyStringArray;
            for (int i = 0; i < a.Length; i++)
            {
                string path = a[i];
                string text = Path.Combine(path, str + ".dll");
                if (File.Exists(text))
                {
                    return AssemblyFilea(text);
                }
            }
            return null;
        }

        public static Assembly AssemblyFilea(string assemblyFile)
        {
            AssemblyName assemblyName = AssemblyName.GetAssemblyName(assemblyFile);
            return AppDomain.CurrentDomain.Load(assemblyName);
        }

        public static void AddAssembly(Assembly assembly)
        {
            //一般情况都项目工程中都有Resources目录 这个方法就是读取该文件夹内的
            string[] manifestResourceNames = assembly.GetManifestResourceNames();
            for (int i = 0; i < manifestResourceNames.Length; i++)
            {
                string text = manifestResourceNames[i];
                //只选择dll
                if (!string.IsNullOrEmpty(text) && text.StartsWith("AssemblyNeutral/") && text.EndsWith(".dll"))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
                    if (!string.IsNullOrEmpty(fileNameWithoutExtension) && !_assemblyConcurrentDictionary.ContainsKey(fileNameWithoutExtension))
                    {
                        Stream manifestResourceStream = assembly.GetManifestResourceStream(text);
                        Assembly value = Astream2(manifestResourceStream, null);
                        _assemblyConcurrentDictionary[fileNameWithoutExtension] = value;
                    }
                }
            }
        }

        private static byte[] Astream(Stream stream)
        {
            MemoryStream memoryStream = stream as MemoryStream;
            if (memoryStream != null)
            {
                return memoryStream.ToArray();
            }
            MemoryStream memoryStream2;
            memoryStream = (memoryStream2 = new MemoryStream());
            byte[] result;
            try
            {
                stream.CopyTo(memoryStream);
                result = memoryStream.ToArray();
            }
            finally
            {
                if (memoryStream2 != null)
                {
                    ((IDisposable)memoryStream2).Dispose();
                }
            }
            return result;
        }

        public static Assembly Astream2(Stream stream, Stream stream2)
        {
            byte[] rawAssembly = Astream(stream);
            byte[] rawSymbolStore = null;
            if (stream2 != null)
            {
                rawSymbolStore = Astream(stream2);
            }
            return AppDomain.CurrentDomain.Load(rawAssembly, rawSymbolStore);
        }

    }

}

