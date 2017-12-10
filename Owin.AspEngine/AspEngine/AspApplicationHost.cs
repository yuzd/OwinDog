namespace Owin.AspEngine
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Web;
    using System.Web.Configuration;

    internal sealed class AspApplicationHost : MarshalByRefObject
    {
        private string _mPath;
        private volatile bool _mUnloading;
        private string _mVPath;
        private AspRequestBroker _requestBroker;


        public AspApplicationHost()
        {
            AppDomain.CurrentDomain.DomainUnload += new EventHandler(this.OnHostDomainUnload);
            try
            {
                this.WebConfigResponseEncoding = this.GetResponseEncodingFromWebConfig();
            }
            catch
            {
                this.WebConfigResponseEncoding = Encoding.UTF8;
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) => 
            null;

        /// <summary>
        /// 从Web.config中的配置节点读取ResponseEncoding
        /// </summary>
        /// <returns></returns>
        private Encoding GetResponseEncodingFromWebConfig()
        {
            try
            {
                GlobalizationSection section = WebConfigurationManager.GetSection("system.web/globalization") as GlobalizationSection;
                return ((section == null) ? Encoding.UTF8 : section.ResponseEncoding);
            }
            catch
            {
                return Encoding.UTF8;
            }
        }

        public override object InitializeLifetimeService() => 
            null;

        /// <summary>
        /// 要求应用程序域退出时
        /// </summary>
        /// <param name="o"></param>
        /// <param name="args"></param>
        private void OnHostDomainUnload(object o, EventArgs args)
        {
            try
            {
                this._mUnloading = true;
                this._requestBroker.DomainUnload();
            }
            catch
            {
            }
        }

        public void Process(AspRequestData req)
        {
            new AspRequestWorker().ProcessRequest(req);
        }

        public void SetRequestBroker(AspRequestBroker broker)
        {
            this._requestBroker = broker;
            AspRequestWorker.Init(this, broker);
        }

        public void UnLoadAppDomain()
        {
            try
            {
                this._mUnloading = true;
                HttpRuntime.UnloadAppDomain();
            }
            catch
            {
            }
        }

        internal AppDomain Domain =>
            AppDomain.CurrentDomain;

        public string Path =>
            (this._mPath ?? (this._mPath = AppDomain.CurrentDomain.GetData(".appPath").ToString()));

        public string VPath =>
            (this._mVPath ?? (this._mVPath = AppDomain.CurrentDomain.GetData(".appVPath").ToString()));

        public Encoding WebConfigResponseEncoding { get; private set; }
    }
}

