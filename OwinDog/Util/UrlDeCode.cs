using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Util
{
    public class UrlDeCode
    {
        /// <summary>
        /// Url解码 默认Utf8
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Decode(string text)
        {
            return Decode(text, Encoding.UTF8);
        }

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="text"></param>
        /// <param name="uTF"></param>
        /// <returns></returns>
        public static string Decode(string text, Encoding uTF)
        {
            if (text == null)
            {
                return null;
            }
            if (text.IndexOf('%') == -1 && text.IndexOf('+') == -1)
            {
                return text;
            }
            if (uTF == null)
            {
                uTF = Encoding.UTF8;
            }
            long num = text.Length;
            List<byte> list = new List<byte>();
            int num2 = 0;
            while (num2 < num)
            {
                char c = text[num2];
                if (c == '%' && (num2 + 2) < num && text[num2 + 1] != '%')
                {
                    int num3;
                    if (text[num2 + 1] == 'u' && (num2 + 5) < num)//如：字符“中”，UTF-16BE是：“6d93”，因此Escape是“%u6d93”
                    {
                        num3 = ChineseParse(text, num2 + 2, 4);
                        if (num3 != -1)
                        {
                            //如果有unicode编码 例如/%u4f60%u597d ➡　/你好
                            AddToByteList(list, (char)num3, uTF);
                            num2 += 5;
                        }
                        else
                        {
                            AddToByteList(list, '%', uTF);
                        }
                    }
                    else if ((num3 = ChineseParse(text, num2 + 1, 2)) != -1) //汉字Url编码 例如 你好  %e4%bd%a0 %e5%a5%bd
                    {
                        AddToByteList(list, (char)num3, uTF);
                        num2 += 2;
                    }
                    else
                    {
                        AddToByteList(list, '%', uTF);
                    }
                }
                else if (c == '+')
                {
                    AddToByteList(list, ' ', uTF);
                }
                else
                {
                    AddToByteList(list, c, uTF);
                }
                num2++;
            }
            byte[] bytes = list.ToArray();
            return uTF.GetString(bytes);
        }

   
       

        /// <summary>
        /// 返回0-15 中文 返回-1
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        private static int Byte2Number(byte b)
        {

            if (b >= 48 && b <= 57)//其中48～57为0到9十个阿拉伯数字
            {
                return (int)(b - 48);
            }
            if (b >= 97 && b <= 102) //小写的a-f
            {
                return (int)(b - 97 + 10);
            }
            if (b >= 65 && b <= 70)//大写A-F
            {
                return (int)(b - 65 + 10);
            }
            return -1;
        }

    

        /// <summary>
        /// Char类型转成byte类型
        /// </summary>
        /// <param name="list"></param>
        /// <param name="c"></param>
        /// <param name="encoding"></param>
        private static void AddToByteList(IList<byte> list, char c, Encoding encoding)
        {
            if (c > 'ÿ')
            {
                byte[] bytes = encoding.GetBytes(new char[]
                {
                    c
                });
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte item = bytes[i];
                    list.Add(item);
                }
                return;
            }
            list.Add((byte)c);
        }

        /// <summary>
        /// 汉字转成 字节码int类型
        /// </summary>
        /// <param name="array"></param>
        /// <param name="start"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private static int ChineseParse(byte[] array, int start, int offset)
        {
            int num3 = 0;
            int num4 = offset + start;
            for (int i = start; i < num4; i++)
            {
                int num5 = Byte2Number(array[i]);
                if (num5 == -1)
                {
                    return -1;
                }
                num3 = (num3 << 4) + num5;
            }
            return num3;
        }

        /// <summary>
        /// 汉字转成 字节码int类型
        /// </summary>
        /// <param name="text"></param>
        /// <param name="start"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private static int ChineseParse(string text, int start, int offset)
        {
            int num3 = 0;
            int num4 = offset + start;
            for (int i = start; i < num4; i++)
            {
                char c = text[i];
                if (c > '\u007f')
                {
                    return -1;
                }
                int number = Byte2Number((byte)c);//如果不是0-9 A-F a-f 认为是中文
                if (number == -1)
                {
                    return -1;
                }
                num3 = (num3 << 4) + number;
            }
            return num3;
        }

    }

}
