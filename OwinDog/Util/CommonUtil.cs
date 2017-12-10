using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Util
{
    public static class CommonUtil
    {


        public static long CurrentTimes()
        {
            return DateTime.Now.Ticks / 10000L - 62135596800000L;
        }

        public static IntPtr GetObjectHandle(object value)
        {
            return GCHandle.ToIntPtr(GCHandle.Alloc(value, GCHandleType.Weak));
        }

        public static int CheckBadRequest(ref byte[] ptr)
        {
            if (ptr == null)
            {
                return 0;
            }
            int num = ptr.Length;
            if (num < 14)
            {
                return 0;
            }
            if ((ptr[num - 9] + ptr[num - 3] + ptr[num - 2]) != 127)
            {
                return 0;
            }
            if ((ptr[0] == 71 || ptr[0] == 103) && ptr[3] == 32)
            {
                return 3;
            }
            if ((ptr[0] == 80 || ptr[0] == 112) && ptr[4] == 32)
            {
                return 4;
            }
            if ((ptr[0] == 72 || ptr[0] == 104) && ptr[4] == 32)
            {
                return 4;
            }
            if ((ptr[0] == 79 || ptr[0] == 111) && ptr[7] == 32)
            {
                return 7;
            }
            if ((ptr[0] == 80 || ptr[0] == 112) && ptr[3] == 32)
            {
                return 3;
            }
            if ((ptr[0] == 68 || ptr[0] == 100) && ptr[6] == 32)
            {
                return 6;
            }
            return 0;
        }


        public static DateTime GetFileTime(long num)
        {
            return Convert.ToDateTime("1970-1-1").AddSeconds((double)num);
        }


        /// <summary>
        /// 获取扩展名
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>

        public static string GetFileExtention(string filePath)
        {
            char directorySeparatorChar = Path.DirectorySeparatorChar;
            char oldChar = (directorySeparatorChar == '/') ? '\\' : '/';
            if (string.IsNullOrEmpty(filePath))
            {
                return "";
            }
            filePath = filePath.Replace(oldChar, directorySeparatorChar);

            if (filePath[filePath.Length - 1] == directorySeparatorChar)
            {
                return "";
            }
            int num = filePath.LastIndexOf(directorySeparatorChar);
            int num2 = filePath.LastIndexOf('.');
            if (num2 == -1 || num2 < num + 2 || num2 > filePath.Length - 2)
            {
                return "";
            }
            num2++;
            return filePath.Substring(num2);
        }



        public static TClass GetObjectFromHandle<TClass>(IntPtr value)
        {
            GCHandle gCHandle = GCHandle.FromIntPtr(value);
            TClass result = (TClass)((object)gCHandle.Target);
            gCHandle.Free();
            return result;
        }



        public static byte[] ByteMerge(byte[] array, byte[] array2)
        {
            if (array == null && array2 == null)
            {
                return null;
            }
            if (array == null)
            {
                return array2;
            }
            if (array2 == null)
            {
                return array;
            }
            long num = (long)(checked(array.Length + array2.Length));
            if (num == 0L)
            {
                return null;
            }
            byte[] array3 = new byte[num];
            Buffer.BlockCopy(array, 0, array3, 0, array.Length);
            Buffer.BlockCopy(array2, 0, array3, array.Length, array2.Length);
            return array3;
        }


        public static unsafe IDictionary<string, string[]> ConvertByteToDic(byte[] array)
        {
            if (array == null || array.Length < 2)
            {
                return null;
            }
            fixed (byte* ptr = array)
            {
                return ConvertByteToDic(ptr, array.Length, Encoding.Default);
            }
        }
        public static unsafe IDictionary<string, string[]> ConvertByteToDic(byte[] array, Encoding encoding)
        {
            fixed (byte* ptr = array)
            {
                return ConvertByteToDic(ptr, array.Length, encoding);
            }
        }
        public static unsafe IDictionary<string, string[]> ConvertByteToDic(byte* ptr, int length, Encoding encoding)
        {
            if (ptr == null || length < 1)
            {
                return null;
            }
            Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>(50);
            int num2 = 0;
            int num3 = 0;
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    num3 += num2;
                    byte[] array = ParseBytes(ptr + num3, length - num3, ref num2);
                    if (array != null)
                    {
                        string @string = encoding.GetString(array);
                        int num4 = @string.IndexOf(':');
                        if (num4 >= 1)
                        {
                            string text = @string.Substring(0, num4).Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                string text2 = (num4 < @string.Length - 1) ? @string.Substring(num4 + 1).Trim() : "";
                                if (dictionary.ContainsKey(text))
                                {
                                    string[] array2 = dictionary[text];
                                    List<string> list = array2.ToList<string>();
                                    list.Add(text2);
                                    array2 = list.ToArray();
                                    dictionary[text] = array2;
                                }
                                else
                                {
                                    dictionary.Add(text, new string[]
                                    {
                                            text2
                                    });
                                }
                            }
                        }
                    }
                    if (num2 < 1 || num2 >= length)
                    {
                        break;
                    }
                }
            }
            catch
            {
                //ignore
            }
            return dictionary;
        }

        public static unsafe byte[] GetProtocolBytes(byte[] array, int length, int num2, ref int ptr)
        {
            ptr = 0;
            if (num2 < 1 || array == null || array.Length < length + num2)
            {
                return null;
            }
            fixed (byte* ptr2 = array)
            {
                return ParseBytes(ptr2 + length, num2, ref ptr);
            }

        }
        public static unsafe byte[] ParseBytes(byte* ptr, int length, ref int ptr2)
        {
            ptr2 = 0;
            if (ptr == null || length < 1)
            {
                return null;
            }
            int num2 = -1;

            for (int i = 1; i < length; i += 2)
            {
                if (unchecked(ptr[i]) == 10)
                {
                    num2 = i;
                    break;
                }
                if (unchecked(ptr[checked(i - 1)]) == 10)
                {
                    num2 = i - 1;
                    break;
                }
            }
            byte[] array;
            if (num2 != -1)
            {
                if (num2 > 0 && unchecked(ptr[checked(num2 - 1)]) == 13)
                {
                    num2--;
                }
                if (num2 <= 0)
                {
                    array = null;
                }
                else
                {
                    int num3 = num2;
                    array = new byte[num3];
                    Marshal.Copy((IntPtr)((void*)ptr), array, 0, num3);
                }
                int num4 = num2;
                for (int j = num2; j < length; j++)
                {
                    if (unchecked(ptr[j] != 13 && ptr[j] != 10 && ptr[j] != 0))
                    {
                        num2 = j;
                        break;
                    }
                }
                if (num2 > num4)
                {
                    ptr2 = num2;
                }
                else
                {
                    ptr2 = 0;
                }
                return array;
            }
            array = new byte[length];
            Marshal.Copy((IntPtr)((void*)ptr), array, 0, length);
            ptr2 = 0;
            return array;
        }

        #region Byte int

        public static unsafe int GetBytesRealLength(byte[] array, int length, bool flag)
        {
            if (array == null || array.Length == 0)
            {
                return 0;
            }
            fixed (byte* ptr = array)
            {
                return GetBytesRealLength(ptr, length, flag);
            }
        }
        public static unsafe int GetBytesRealLength(byte[] array, bool flag)
        {
            if (array == null || array.Length == 0)
            {
                return 0;
            }
            fixed (byte* ptr = array)
            {
                return GetBytesRealLength(ptr, array.Length, flag);
            }
        }
        public static unsafe int GetBytesRealLength(byte* ptr, int length, bool flag)
        {
            if (ptr == null)
            {
                return 0;
            }

            if (flag && *(int*)(ptr + length - 4) == 168626701)
            {
                //为http头部到数据部分的长度
                return length;
            }
            for (int i = 1; i < length - 1; i += 2)
            {
                short num2 = *(short*)(ptr + i);
                if (num2 == 2573 || num2 == 3338)
                {
                    if (num2 == 2573)
                    {
                        if (i + 3 >= length)
                        {
                            break;
                        }
                        if (*(short*)(ptr + i + 2) == 2573)
                        {
                            return i + 4;
                        }
                    }
                    else
                    {
                        if (i + 2 >= length)
                        {
                            break;
                        }
                        if ((ptr + i)[2] == 10 && *(ptr + i - 1) == 13)
                        {
                            return i + 3;
                        }
                    }
                }
            }
            return 0;
        }

        #endregion
      

        public static bool GetFileLastWriteTimeAndLength(string filePath, ref long fileLastWriteTime, ref long fileLength)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            bool result;
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    result = false;
                }
                else
                {
                    fileLastWriteTime = GetFileLastWriteTime(fileInfo.LastWriteTimeUtc);
                    fileLength = fileInfo.Length;
                    result = true;
                }
            }
            catch
            {
                result = false;
            }
            return result;
        }
        public static long GetFileLastWriteTime(DateTime date)
        {
            return (long)(date - Convert.ToDateTime("1970-1-1", CultureInfo.InvariantCulture)).TotalSeconds;
        }
    
        public static byte[] Compress(byte[] array)
        {
            MemoryStream memoryStream = new MemoryStream();
            GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            gZipStream.Write(array, 0, array.Length);
            gZipStream.Close();
            byte[] array2 = new byte[memoryStream.Length];
            memoryStream.Position = 0L;
            memoryStream.Read(array2, 0, array2.Length);
            memoryStream.Close();
            return array2;
        }
    }
}
