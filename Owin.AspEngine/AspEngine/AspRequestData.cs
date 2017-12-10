namespace Owin.AspEngine
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    [Serializable]
    internal class AspRequestData
    {
        public AspRequestData(int reqid, IDictionary<string, object> env)
        {
            this.RequestId = reqid;
            this.RequestHttpHeader = env["owin.RequestHeaders"] as IDictionary<string, string[]>;
            this.QueryString = env["owin.RequestQueryString"] as string;
            this.Verb = env["owin.RequestMethod"] as string;
            this.Protocol = env["owin.RequestProtocol"] as string;
            this.UrlPath = env["owin.RequestPath"] as string;
            this.RemoteAddress = env["server.RemoteIpAddress"] as string;
            this.RemotePort = int.Parse((string) env["server.RemotePort"]);
            this.LocalAddress = env["server.LocalIpAddress"] as string;
            this.LocalPort = int.Parse((string) env["server.LocalPort"]);
        }

        public string LocalAddress { get; private set; }

        public int LocalPort { get; private set; }

        public string Protocol { get; private set; }

        public string QueryString { get; private set; }

        public string RemoteAddress { get; private set; }

        public int RemotePort { get; private set; }

        /// <summary>
        /// 请求头
        /// </summary>
        public IDictionary<string, string[]> RequestHttpHeader { get; private set; }

        public int RequestId { get; private set; }

        public string UrlPath { get; private set; }

        public string Verb { get; private set; }
    }
}

