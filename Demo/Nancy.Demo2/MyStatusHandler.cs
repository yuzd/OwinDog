using Nancy;
using Nancy.ViewEngines;
using Nancy.ErrorHandling;


namespace Nancy.Demo2
{

    /// <summary>
    /// 自定义http status处理类
    /// </summary>
    public class MyStatusHandler : IStatusCodeHandler
    {
        private IViewRenderer viewRenderer;

        public MyStatusHandler(IViewRenderer viewRenderer)
        {
            this.viewRenderer = viewRenderer;
        }

        /// <summary>
        /// 当前状态是否需要自己处理
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool HandlesStatusCode(HttpStatusCode statusCode, NancyContext context)
        {
            //return false;
            return (statusCode == HttpStatusCode.NotFound //如：404状态码（找不到网页）让自己处理
                //|| statusCode == HttpStatusCode.ServiceUnavailable
                //|| statusCode == HttpStatusCode.InternalServerError
                );
        }

        /// <summary>
        /// 具体处理过程
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="context"></param>
        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            var response = viewRenderer.RenderView(context, "Status/404",null); //指定专用的cshtml文件
            response.StatusCode = statusCode == HttpStatusCode.NotFound ? HttpStatusCode.OK : statusCode;
            context.Response = response;
        }
    }


}