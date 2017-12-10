using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Nancy.Session;
namespace Nancy.Demo2
{
    public class BootStrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoc.TinyIoCContainer container, Bootstrapper.IPipelines pipelines)
        {
            //启用session
            CookieBasedSessions.Enable(pipelines);
        }
    }
}