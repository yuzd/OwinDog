using System;
using System.Collections.Generic;
using System.Linq;

namespace Service
{
    /// <summary>
    /// 循环利用
    /// </summary>
    internal static class RequestDataFactory
    {
        private const int MaxSize = 10000;

        private static readonly Queue<byte[]> AQueue = new Queue<byte[]>(MaxSize);

        /// <summary>
        /// 获取
        /// </summary>
        /// <returns></returns>
        public static byte[] Create()
        {
            byte[] result;
            lock (AQueue)
            {
                if (AQueue.Count < 1)
                {
                    //TcpClient.ReceiveBufferSize Property  The size of the receive buffer, in bytes. The default value is 8192 bytes.
                    result = new byte[8192];
                }
                else
                {
                    result = AQueue.Dequeue();
                }
            }
            return result;
        }

        /// <summary>
        /// 回收
        /// </summary>
        /// <param name="array"></param>
        public static void Recover(byte[] array)
        {
            if (array == null || array.Length != 8192 || AQueue.Count > MaxSize)
            {
                return;
            }
            lock (AQueue)
            {
                AQueue.Enqueue(array);
            }
        }

    }
}
