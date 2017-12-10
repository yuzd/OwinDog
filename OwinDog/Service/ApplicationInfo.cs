using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Util;
using OwinEngine;

namespace Service
{
    public static class ApplicationInfo
    {
        public const string ServerInfo = "OwinDog/1.0";

        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static string AppPtah { get; set; }

        /// <summary>
        /// 默认是 owindog.exe 所在的 site\wwwroot 目录 放置网站运行程序的
        /// </summary>
        public static string Wwwroot { get; set; }

        public static string Approot { get; set; }

        public static OwinAdapter OwinAdapter { get; set; }


        public static void SetApplicationPath(string appPath, string rootPath)
        {
            // -root 网站或webapi的物理路径，如 tinyfox -root d:\myapi\wwwroot。
            //（不加该参数时，默认路径是tinyfox.exe所在文件夹内的site\wwwroot目录）
            AppPtah = (appPath);
            bool isWindows = SystemUtil.IsWindowOs();
            if (string.IsNullOrEmpty(rootPath))
            {
                Wwwroot = (Path.Combine(appPath, "site", "wwwroot"));
                Approot = (Path.Combine(appPath, "site", "approot"));
                return;
            }
            if ((!isWindows && rootPath[0] != '/') || (isWindows && rootPath.Length > 1 && rootPath[1] != ':'))
            {
                string root = Path.Combine(AppPtah, rootPath);
                root = Path.GetFullPath(root);
                Wwwroot = (root);
                DirectoryInfo directoryInfo = new DirectoryInfo(Wwwroot);
                string fullName = directoryInfo.Parent.FullName;
                root = Path.Combine(fullName, "approot");
                if (Directory.Exists(root))
                {
                    Approot = (root);
                }
            }
            else
            {
                Wwwroot = rootPath;
                string fullName;
                try
                {
                    fullName = new DirectoryInfo(rootPath).Parent.FullName;
                }
                catch
                {
                    throw new IOException(string.Format("Error. Path: {0}", rootPath));
                }
               
                string root = Path.Combine(fullName, "approot");
                if (Directory.Exists(root))
                {
                    Approot = root;
                }
            }
        }



    }



}

