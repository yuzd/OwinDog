using Owin;
using Nancy;

namespace Nancy.Demo2
{

    /// <summary>
    /// 支持NancyFx的OWIN启动类
    /// <para>MS'OWIN 标准的宿主都需要一个启动类</para>
    /// </summary>
    public class Startup
    {
        public Startup() {

            // 显示详细的异常信息
            StaticConfiguration.DisableErrorTraces = false;
            
            //增加Nancy处理json字串的长度
            //Nancy.Json.JsonSettings.MaxJsonLength = int.MaxValue;
        
            // 其它初始化动作
            // ........
        }

        public void Configuration(IAppBuilder builder)
        {
            //将 Nancy(中间件)添加到Microsoft.Owin处理环节中
            ////////////////////////////////////////////////////
            builder.UseNancy();

        }
    }


}
