using System;
using System.Collections.Concurrent;

namespace Service
{
  
    public static class RequestCheck
    {
        private static string[] NotSafeArray = new string[]
           {
                    "/bin",
                    "/views",
                    "/app_code",
                    "/app_data"
           };

        private static readonly ConcurrentDictionary<string, bool> SafeRequestUrlDic = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// 测试非法地址
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static bool IsNotSafeRequest(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }
            bool flag = false;
            if (SafeRequestUrlDic.TryGetValue(url, out flag))
            {
                return flag;
            }
            flag = true;
            int num = url.IndexOf('/', 1);
            if (num < 3)
            {
                return true;
            }
            string path = url.Substring(0, num);
            for (int i = 0; i < NotSafeArray.Length; i++)
            {
                string item = NotSafeArray[i];
                if (string.Equals(path, item, StringComparison.OrdinalIgnoreCase))
                {
                    flag = false;
                    break;
                }
            }
            if (flag && url.IndexOf("/.") != -1)
            {
                flag = false;
            }
            if (flag && url.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
            {
                flag = false;
            }
            if (flag && url.EndsWith(".asax", StringComparison.OrdinalIgnoreCase))
            {
                flag = false;
            }
            SafeRequestUrlDic[url] = flag;
            return flag;
        }

        /// <summary>
        /// 排除
        /// </summary>
        /// <param name="key"></param>
        public static void Expect(string key)
        {
            SafeRequestUrlDic[key] = false;
        }


    }
}
