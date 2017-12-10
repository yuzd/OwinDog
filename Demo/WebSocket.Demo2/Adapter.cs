/***************************************************************
 *        WebSocket 应用示例  之二
 * =============================================================   
 * 本DEMO的目的意义：
 *  演示封装一个 WebSocket 对象
 *  
 *  使用方法：将编译得到的dll放到网站的bin文件夹中。
 * *************************************************************/


#region <USING>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;

#endregion


namespace WebSocket.Demo
{

    /// <summary>
    /// owin/owindog For OWIN 接口类
    /// </summary>
    public class Adapter
    {


        /// <summary>
        /// OWIN适配器的主函数
        /// </summary>
        /// <param name="env"></param>
        /// <returns></returns>
        public Task OwinMain(IDictionary<string, object> env)
        {
            //是否包含Websocket握手函数并尝试进行WebSocket连接
            if (env.ContainsKey("websocket.Accept"))
            {
                var websocket = new WebSocket(env);

                //if(websocket.RequestPath == ......)

                if (websocket.Accept())
                {

                    websocket.OnSend = OnSend;
                    websocket.OnClose = OnClose;
                    websocket.OnRead = OnRead;


                    // ..... 
                    // websocket.RemoteIpAddress
                    // .....
                    // ......

                    //开始接受远端数据
                    //本方法只需在连接成功后调用一次。
                    websocket.StartRead();

                    //返回表示完成的任务
                    return Task.Delay(0);
                }
            }

            //如果不是websocket请求，就接普通OWIN处理
            return ProcessRequest(env);
        }



        /// <summary>
        /// 数据接收事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        void OnRead(object sender, string message)
        {
            var websocket = sender as WebSocket;

            if (message == "exit" || message == "close")
            {
                websocket.Close();
                return;
            }

            websocket.Send(message);
        }

        
        /// <summary>
        /// 数据发送完成的事件
        /// </summary>
        /// <param name="sender"></param>
        void OnSend(object sender)
        {
            /// ..... ////
        }

        
        /// <summary>
        /// 连接已经关闭
        /// </summary>
        /// <param name="sender"></param>
        void OnClose(object sender)
        {
            // ... ... //
        }







        /// <summary>
        /// 普通OWIN请求的处理函数
        /// </summary>
        /// <param name="env"></param>
        /// <returns></returns>
        private Task ProcessRequest(IDictionary<string, object> env)
        {

            // 从字典中获取向客户（浏览器）发送数据的“流”对象
            /////////////////////////////////////////////////////////
            var responseStream = env["owin.ResponseBody"] as Stream;

            // 你准备发送的数据
            const string outString = "<html><head><title>Owin Server</title></head><body>Owin Server!<br /><h2>Owin Server，放飞您灵感的翅膀...</h2>\r\n</body></html>";
            var outBytes = Encoding.UTF8.GetBytes(outString);

            // 从参数字典中获取Response HTTP头的字典对象
            var responseHeaders = env["owin.ResponseHeaders"] as IDictionary<string, string[]>;

            // 设置必要的http响应头
            ////////////////////////////////////////////////////////////////

            // 设置 Content-Type头
            responseHeaders.Add("Content-Type", new[] { "text/html; charset=utf-8" });


            // 把正文写入流中，发送给浏览器
            responseStream.Write(outBytes, 0, outBytes.Length);

            return Task.FromResult<int>(0);

        }


    }



}
