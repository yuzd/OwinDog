using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace OwinEngine
{
   
	public interface ISocket
	{
        /// <summary>
        /// 获取访问者IP地址
        /// </summary>
        /// <returns></returns>
		string GetRemoteIpAddress();

        /// <summary>
        /// 获取访问者的Ip端口
        /// </summary>
        /// <returns></returns>
		int GetRemoteIpPort();

        /// <summary>
        /// 获取本地的IP地址
        /// </summary>
        /// <returns></returns>
		string LocalIpAddress();

        /// <summary>
        /// 获取本地的IP端口
        /// </summary>
        /// <returns></returns>
		int LocalIpPort();

        /// <summary>
        /// 写操作
        /// </summary>
        /// <param name="callBack"></param>
        /// <param name="state"></param>
		void Read(Action<OwinSocket, byte[], int, Exception, object> callBack, object state);

        /// <summary>
        /// 读操作
        /// </summary>
        /// <param name="array"></param>
        /// <param name="callback"></param>
        /// <param name="otherState"></param>
		void Write(byte[] array, Action<OwinSocket, int, Exception, object> callback, object otherState);

		void WriteForPost(byte[] headDomain, byte[] body, Action<OwinSocket, int, Exception, object> callback, object otherState);

		void Dispose();
	}
}
