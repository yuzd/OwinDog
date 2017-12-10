using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace OwinEngine
{
    /// <summary>
    /// http://www.cnblogs.com/smark/archive/2012/11/26/2789812.html
    /// </summary>
    internal class WebSocketReciveDataParse
    {
        private readonly Queue<ReciveData> _reciveDataQueue = new Queue<ReciveData>();

        private byte[] _buffs;

        public bool IsEof { get; set; }

        public bool abool { get; set; }

        public ReciveData GetReciveData()
        {
            ReciveData result;
            lock (_reciveDataQueue)
            {
                if (_reciveDataQueue.Count < 1)
                {
                    result = null;
                }
                else
                {
                    result = _reciveDataQueue.Dequeue();
                }
            }
            return result;
        }



        public void Parse(byte[] array, int srcOffset, int reciveLength)
        {

            if (_buffs != null)
            {
                byte[] tempBytes = new byte[_buffs.Length + reciveLength];
                Buffer.BlockCopy(_buffs, 0, tempBytes, 0, _buffs.Length);
                Buffer.BlockCopy(array, srcOffset, tempBytes, _buffs.Length, reciveLength);
                array = tempBytes;
                _buffs = null;
                srcOffset = 0;
                reciveLength = tempBytes.Length;
            }
            if (reciveLength < 2)
            {
                Fill(array, srcOffset, reciveLength);
                return;
            }
            if (reciveLength > 8388608)//最大值也是8388608bytes(8M)
            {
                throw new Exception("WebSocket Error: Recvive Data Too Long...");
            }
            int index = srcOffset;
            //第一个字节 最高位用于描述消息是否结束,如果为1则该消息为消息尾部,如果为零则还有后续数据包
            int firstByte = array[0] >> 4;//向右移动四位 array[0]=128
            if (firstByte != 8 && firstByte != 0)
            {
                IsEof = (true);
                return;
            }
            bool flag = firstByte == 8;
            int num5 = array[index] & 15;//00001111  1111 0000 0001 0011 0111 1111 0010 0111 0110 0101  清掉X的高4位,而保留 低 4位 最低4位用于描述消息类型,消息类型暂定有15种
            index++;
            bool flag2 = array[index] >> 7 > 0; 
            ushort num6 = (ushort)(array[index] & 127);//datalength
            index++; //如果  == 126  start = start + 2  == 127 start = start + 8
            ulong num7;
            if (num6 < 126)
            {
                num7 = num6;
            }
            else
            {
                if (num6 != 126)
                {
                    abool = (true);
                    IsEof = (true);
                    return;
                }
                if (Nokori(srcOffset, reciveLength, index) < 2)
                {
                    Fill(array, srcOffset, reciveLength);
                    return;
                }
                num7 = (ulong)((array[index] << 8) + array[index + 1]);
                index += 2;
            }
            byte[] array3 = null;
            if (flag2)//是否有 第二位 masks  如果存在掩码的情况下获取4位掩码值:
            {
                if (Nokori(srcOffset, reciveLength, index) < 4)
                {
                    Fill(array, srcOffset, reciveLength);
                    return;
                }
                array3 = new byte[4];
                Buffer.BlockCopy(array, index, array3, 0, 4);
                index += 4;
            }
            if (Nokori(srcOffset, reciveLength, index) < (long)num7)
            {
                _buffs = new byte[reciveLength];
                Buffer.BlockCopy(array, srcOffset, _buffs, 0, reciveLength);//数据分批读取
                return;
            }
            byte[] array4 = new byte[num7];
            Buffer.BlockCopy(array, index, array4, 0, (int)num7);
            index += (int)num7;
            if (flag2)
            {
                for (int i = 0; i < array4.Length; i++)
                {
                    array4[i] ^= array3[i % 4];//获取消息体
                }
            }
            if (num5 == 8) //1000
            {
                IsEof = (true);
            }
            if (num5 == 9) // 1001
            {
                IsEof = (true);
                return;
            }
            if (num5 != 10) //1010
            {
                lock (_reciveDataQueue)
                {
                    ReciveData item = new ReciveData
                    {
                        Data = array4,
                        IsEof = flag,
                        BytesLength = (int)num7,
                        Type = num5
                    };
                    _reciveDataQueue.Enqueue(item);
                }
            }
            int num8 = Nokori(srcOffset, reciveLength, index);
            if (num8 < 1)
            {
                return;
            }
            Parse(array, index, num8);
        }

        private void Fill(byte[] src, int srcOffset, int index)
        {
            _buffs = new byte[index];
            Buffer.BlockCopy(src, srcOffset, _buffs, 0, index);
        }

        private static int Nokori(int srcOffset, int reciveLength, int index)
        {
            return (srcOffset + reciveLength - index);
        }



        public class ReciveData
        {
            public byte[] Data;
            /// <summary>
            /// 第三分量，表示有效数据的长度
            /// </summary>

            public int BytesLength;

            public bool IsEof;

            /// <summary>
            /// //数据类型，只能是1、2、8   1表示本文数据，2表示二进制数据，8表示对方关闭连接）
            /// </summary>
            public int Type; 
        }
    }

}
