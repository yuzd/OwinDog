
using Model;

namespace Util
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;

    public static class SystemUtil
    {
        private static unsafe string GetUname()
        {
            byte[] array = new byte[8192];
            string result;
            try
            {
                try
                {
                    fixed (byte* ptr = array)
                    {
                        if (uname((IntPtr)((void*)ptr)) == 0)
                        {
                            result = Marshal.PtrToStringAnsi((IntPtr)((void*)ptr));
                            return result;
                        }
                    }
                }
                finally
                {
                    byte* ptr = null;
                }
                result = string.Empty;
            }
            catch
            {
                result = string.Empty;
            }
            return result;
        }

        public static bool IsWindowOs()
        {
            int platform = (int)Environment.OSVersion.Platform;
            return platform != 4 && platform != 6 && platform != 128;
        }

        public static void Init(LibUv b1)
        {
            if (b1.IsWindows)
            {
                InitLibWindows.InitLib(b1);
                return;
            }
            InitLibUnix.InitLib(b1);
        }

        public static bool IsDarwin()
        {
            return string.Equals(GetUname(), "Darwin", StringComparison.Ordinal);
        }


        [DllImport("libc", EntryPoint = "uname")]
        private static extern int uname(IntPtr entry);
        public static class InitLibUnix
        {

            public static void InitLib(LibUv libUv)
            {
                libUv.LoadLibrary = new Func<string, IntPtr>(LoadLibrary);
                libUv.FreeLibrary = new Func<IntPtr, bool>(FreeLibrary);
                libUv.GetProcAddress = new Func<IntPtr, string, IntPtr>(GetProcAddress);
            }

            public static bool FreeLibrary(IntPtr ptr1)
            {
                return (dlclose(ptr1) == 0);
            }

            public static IntPtr GetProcAddress(IntPtr ptr1, string text1)
            {
                dlerror();
                IntPtr ptr = dlsym(ptr1, text1);
                if (!(dlerror() == IntPtr.Zero))
                {
                    return IntPtr.Zero;
                }
                return ptr;
            }

            public static IntPtr LoadLibrary(string text1)
            {
                return dlopen(text1, 2);
            }
           

            [SuppressUnmanagedCodeSecurity, DllImport("__Internal", EntryPoint="dlclose", SetLastError=true)]
            public static extern int dlclose(IntPtr aa);
           

            [SuppressUnmanagedCodeSecurity, DllImport("__Internal", EntryPoint = "dlerror", SetLastError = true)]
            public static extern IntPtr dlerror();

            [SuppressUnmanagedCodeSecurity, DllImport("__Internal", EntryPoint="dlsym", SetLastError=true)]
            public static extern IntPtr dlsym(IntPtr aa, string bb);

            [SuppressUnmanagedCodeSecurity, DllImport("__Internal", EntryPoint="dlopen", SetLastError=true)]
            public static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPStr)] string aa, int bb);
        }

        public static class InitLibWindows
        {
            public static void InitLib(LibUv libUv)
            {
                libUv.LoadLibrary = new Func<string, IntPtr>(LoadLibrary);
                libUv.FreeLibrary = new Func<IntPtr, bool>(FreeLibrary);
                libUv.GetProcAddress = new Func<IntPtr, string, IntPtr>(GetProcAddress);
            }

            [DllImport("kernel32", EntryPoint="FreeLibrary")]
            public static extern bool FreeLibrary(IntPtr lib);

            [DllImport("kernel32", EntryPoint="LoadLibrary")]
            public static extern IntPtr LoadLibrary(string lib);

            [DllImport("kernel32", EntryPoint="GetProcAddress", CharSet=CharSet.Ansi, SetLastError=true, ExactSpelling=true)]
            public static extern IntPtr GetProcAddress(IntPtr p, string a);
        }
    }
}

