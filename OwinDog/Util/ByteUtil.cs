using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Util
{
    public class ByteUtil
    {
        private static readonly byte[] _endByts = new byte[]
        {
            48,
            13,
            10,
            13,
            10
        };

        public static byte[] GetEndByts()
        {
            return _endByts;
        }

        public static byte[] StrToByte(string text)
        {
            return StrToByte(text, Encoding.Default);
        }
        public static byte[] StrToByte(string s, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(s);
            return BytesOffset(bytes, 0, bytes.Length);
        }
        public static byte[] BytesOffset(byte[] array, int srcOffset, int num)
        {
            if (array == null || array.Length < 1 || num == 0)
            {
                return _endByts;
            }
            string text = num.ToString("x", CultureInfo.InvariantCulture);
            int num2 = text.Length + num + 4;
            byte[] array2 = new byte[num2];
            int dstOffset = PullLengthInfo(array2, text);
            Buffer.BlockCopy(array, srcOffset, array2, dstOffset, num);
            array2[num2 - 2] = 13;
            array2[num2 - 1] = 10;
            return array2;

        }

        public static byte[] IntToByte(int num)
        {
            string text = num.ToString("x", CultureInfo.InvariantCulture);
            int length = text.Length;
            byte[] array = new byte[length + 2];
            for (int i = 0; i < length; i++)
            {
                array[i] = (byte)text[i];
            }
            array[length++] = 13;
            array[length] = 10;
            return array;

        }

        private static int HexCharToInt(IEnumerable<byte> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException("enumerable");
            }
            int num = 0;
            checked
            {
                foreach (byte current in enumerable)
                {
                    if (current <= 47 || current >= 58)
                    {
                        if (current <= 64 || current >= 71)
                        {
                            if (current <= 96 || current >= 103)
                            {
                                throw new FormatException();
                            }
                            num = (num << 8) + (int)(current - 87);
                        }
                        else
                        {
                            num = (num << 8) + (int)(current - 55);
                        }
                    }
                    else
                    {
                        num = (num << 8) + (int)(current - 48);
                    }
                }
                return num;
            }
        }


        private static int PullLengthInfo(byte[] body, string strLen)
        {
            int length = strLen.Length;
            for (int i = 0; i < length; i++)
            {
                body[i] = (byte)strLen[i];
            }
            body[length++] = 13;
            body[length++] = 10;
            return length;
        }



        public class Decode
        {

            private byte[] _lastBytes;


            private bool BadFormat { get; set; }

            private bool IsComplete { get; set; }

            public void Init()
            {
                _lastBytes = null;
                IsComplete = (false);
                BadFormat = (false);
            }


            public byte[] Deocde(byte[] array)
            {
                return Deocde(array, 0, array.Length);
            }

            public byte[] Deocde(byte[] array, int num, int num2)
            {
                if (IsComplete || BadFormat)
                {
                    return null;
                }

                int num4;
                if (num2 + ((_lastBytes == null) ? 0 : _lastBytes.Length) >= 5)
                {
                    byte[] array2 = array;
                    if (_lastBytes != null)
                    {
                        array2 = new byte[_lastBytes.Length + num2];
                        Buffer.BlockCopy(_lastBytes, 0, array2, 0, _lastBytes.Length);
                        Buffer.BlockCopy(array, num, array2, _lastBytes.Length, num2);
                        _lastBytes = null;
                        num = 0;
                        num2 = array2.Length;
                    }
                    byte[] array3 = null;
                    while (true)
                    {
                        DecodResult decodResult = RealDeocde(array2, num, num2);
                        if (decodResult.Info == DecodState.Ok)
                        {
                            int num3 = decodResult.Data.Length;
                            if (array3 == null)
                            {
                                array3 = new byte[num3];
                                Buffer.BlockCopy(decodResult.Data, 0, array3, 0, num3);
                            }
                            else
                            {
                                num4 = array3.Length;
                                Array.Resize<byte>(ref array3, num4 + num3);
                                Buffer.BlockCopy(decodResult.Data, 0, array3, num4, num3);
                            }
                            num += decodResult.ChunkedLen;
                            num2 -= decodResult.ChunkedLen;
                        }
                        else
                        {
                            if (decodResult.Info == DecodState.Complete)
                            {
                                break;
                            }
                            if (decodResult.Info == DecodState.Err)
                            {
                                goto Block_9;
                            }
                            if (decodResult.Info == DecodState.WaitNext)
                            {
                                goto Block_10;
                            }
                        }
                    }
                    return array3;
                    Block_9:
                    return null;
                    Block_10:
                    _lastBytes = new byte[num2];
                    Buffer.BlockCopy(array2, num, _lastBytes, 0, num2);
                    return array3;
                }
                if (_lastBytes == null)
                {
                    _lastBytes = new byte[num2];
                    Buffer.BlockCopy(array, num, _lastBytes, 0, num2);
                    return null;
                }
                num4 = _lastBytes.Length;
                Array.Resize<byte>(ref _lastBytes, num4 + num2);
                Buffer.BlockCopy(array, num, _lastBytes, num4, num2);
                return null;
            }

            private DecodResult RealDeocde(byte[] data, int offset, int size)
            {
                int num3 = 0;

                for (int i = 0; i < ((size > 6) ? 7 : size); i++)
                {
                    if (data[i + offset] == 13 && data[i + offset + 1] == 10)
                    {
                        num3 = i;
                        break;
                    }
                }
                if (num3 < 1)
                {
                    DecodResult decodResult = new DecodResult
                    {
                        Info = DecodState.Err
                    };
                    BadFormat = (true);
                    return decodResult;
                }
                byte[] array2 = new byte[num3];
                Buffer.BlockCopy(data, offset, array2, 0, array2.Length);
                int num4;
                try
                {
                    num4 = HexCharToInt(array2);
                }
                catch
                {
                    DecodResult decodResult = new DecodResult
                    {
                        Info = DecodState.Err
                    };
                    BadFormat = (true);
                    var result = decodResult;
                    return result;
                }
                if (num4 == 0)
                {
                    if (data[offset + size - 2] == 13 && data[offset + size - 1] == 10)
                    {
                        IsComplete = (true);
                        return new DecodResult
                        {
                            Info = DecodState.Complete
                        };
                    }
                    return new DecodResult
                    {
                        Info = DecodState.WaitNext
                    };
                }
                else
                {
                    if (num3 + 2 + num4 + 2 > size)
                    {
                        return new DecodResult
                        {
                            Info = DecodState.WaitNext
                        };
                    }
                    int num5 = num3 + 2 + num4 + offset;
                    if (data[num5] != 13 && data[num5 + 1] != 10)
                    {
                        DecodResult decodResult = default(DecodResult);
                        BadFormat = (true);
                        decodResult.Info = DecodState.Err;
                        return decodResult;
                    }
                    byte[] array3 = new byte[num4];
                    Buffer.BlockCopy(data, offset + num3 + 2, array3, 0, array3.Length);
                    int chunkedLen = num3 + 2 + num4 + 2;
                    return new DecodResult
                    {
                        Info = DecodState.Ok,
                        Data = array3,
                        ChunkedLen = chunkedLen
                    };
                }
            }



            private struct DecodResult
            {
                public DecodState Info;

                public byte[] Data;

                public int ChunkedLen;
            }

            private enum DecodState
            {
                Ok,
                Err,
                WaitNext,
                Complete
            }
        }
    }
}
