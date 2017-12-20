
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Owin.AspEngine;

namespace AspTest
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

            return AspNet.Process(env);
        }





        


    }



}
