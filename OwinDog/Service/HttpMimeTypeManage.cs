using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Service
{
    public static class HttpMimeTypeManage
    {
        private static readonly Dictionary<string, string> _httpMimeTypes;
        static HttpMimeTypeManage()
        {
            _httpMimeTypes = new Dictionary<string, string>();
            Add("htm", "text/html");
            Add("html", "text/html");
            Add("asp", "text/html");
            Add("aspx", "text/html");
            Add("vbs", "text/html");
            Add("stm", "text/html");
            Add("shtm", "text/html");
            Add("shtml", "text/html");
            Add("hxt", "text/html");
            Add("php", "text/html");
            Add("htt", "text/webviewhtml");
            Add("rm", "application/vnd.rn-realmedia");
            Add("rmvb", "application/vnd.rn-realmedia");
            Add("ra", "audio/x-pn-realaudio");
            Add("ram", "audio/x-pn-realaudio");
            Add("rpm", "audio/x-pn-realaudio-plugin");
            Add("wmv", "audio/wav");
            Add("asf", "video/x-ms-asf");
            Add("asr", "video/x-ms-asf");
            Add("asx", "video/x-ms-asf");
            Add("wmf", "application/x-msmetafile");
            Add("au", "audio/basic");
            Add("mpg", "video/mpeg");
            Add("dat", "video/mpeg");
            Add("vob", "video/mpeg");
            Add("mpeg", "video/mpeg");
            Add("doc", "application/msword");
            Add("docx", "application/msword");
            Add("dot", "application/msword");
            Add("wps", "application/vnd.ms-works");
            Add("wri", "application/x-mswrite");
            Add("xla", "application/vnd.ms-excel");
            Add("xlc", "application/vnd.ms-excel");
            Add("xlm", "application/vnd.ms-excel");
            Add("xls", "application/vnd.ms-excel");
            Add("xlt", "application/vnd.ms-excel");
            Add("xlw", "application/vnd.ms-excel");
            Add("pdf", "application/pdf");
            Add("ppt", "application/vnd.ms-powerpoint");
            Add("pot", "application/vnd.ms-powerpoint");
            Add("rtf", "application/rtf");
            Add("rtx", "text/richtext");
            Add("crt", "application/x-x509-ca-cert");
            Add("der", "application/x-x509-ca-cert");
            Add("movie", "video/x-sgi-movie");
            Add("mp3", "audio/mpeg");
            Add("mp2", "audio/mpeg");
            Add("mpe", "audio/mpeg");
            Add("mpa", "audio/mpeg");
            Add("mp4", "video/mp4");
            Add("mov", "video/quicktime");
            Add("qt", "video/quicktime");
            Add("qtl", "application/x-quicktimeplayer");
            Add("avi", "video/x-msvideo");
            Add("swf", "application/x-shockwave-flash");
            Add("flv", "flv-application/octet-stream");
            Add("mid", "audio/mid");
            Add("midi", "audio/mid");
            Add("rmi", "audio/mid");
            Add("xamp", "application/xaml+xml");
            Add("xaml", "application/xaml+xml");
            Add("xap", "application/x-silverlight-app");
            Add("mf", "application/manifest");
            Add("manifest", "application/manifest");
            Add("application", "application/x-ms-application");
            Add("xbap", "application/x-ms-xbap");
            Add("deploy", "application/octet-stream");
            Add("xps", "application/vnd.ms-xpsdocument");
            Add("xml", "text/xml");
            Add("xsl", "text/xml");
            Add("xslt", "text/xml");
            Add("xsd", "text/xml");
            Add("xsf", "text/xml");
            Add("dtd", "text/xml");
            Add("ism", "text/xml");
            Add("ismc", "text/xml");
            Add("js", "application/x-javascript");
            Add("json", "application/json");
            Add("txt", "text/plain");
            Add("bas", "text/plain");
            Add("c", "text/plain");
            Add("h", "text/plain");
            Add("cpp", "text/plain");
            Add("pas", "text/plain");
            Add("sh", "application/x-sh");
            Add("mht", "message/rfc822");
            Add("mhtml", "message/rfc822");
            Add("eml", "message/rfc822");
            Add("news", "message/rfc822");
            Add("jpe", "image/jpeg");
            Add("jpg", "image/jpeg");
            Add("jpeg", "image/jpeg");
            Add("gif", "image/gif");
            Add("bmp", "image/bmp");
            Add("ico", "image/x-ico");
            Add("tif", "image/tiff");
            Add("tiff", "image/tiff");
            Add("png", "image/png");
            Add("pnz", "image/png");
            Add("svg", "image/svg+xml");
            Add("woff", "application/x-font-woff");
            Add("woff2", "application/x-font-woff");
            Add("ttf", "application/x-font-truetype");
            Add("otf", "application/x-font-opentype");
            Add("eot", "application/vnd.ms-fontobject");
            Add("css", "text/css");
            Add("wml", "text/vnd.wap.wml");
            Add("wmlc", "application/vnd.wap.wmlc");
            Add("wmls", "text/vnd.wap.wmlscript");
            Add("wmlsc", "application/vnd.wap.wmlscriptc");
            Add("wbmp", "image/vnd.wap.wbmp");
            Add("wsc", "application/vnd.wap/wmlscriptc");
            Add("jad", "text/vnd.sun.j2me.app-descriptor");
            Add("jar", "application/java-archive");
            Add("sis", "application/vnd.symbian.install");
            Add("amr", "audio/amr");
            Add("pmd", "audio/pmd");
            Add("3gp", "video/3gpp");
            Add("ogg", "audio/ogg");
            Add("flac", "audio/flac");
            Add("aac", "audio/aac");
            Add("webm", "video/webm");
            Add("gz", "application/x-gzip");
            Add("bz2", "application/x-bzip2");
            Add("tar", "application/x-tar");
            Add("gtar", "application/x-gtar");
            Add("tgz", "application/x-compressed");
            Add("zip", "application/x-zip-compressed");
            Add("java", "application/octet-stream");
            Add("jpb", "application/octet-stream");
            Add("jck", "application/liquidmotion");
            Add("jcz", "application/liquidmotion");
            Add("apk", "application/vnd.android.package-archive");
            Add("ipa", "application/iphone");
            Add("pxl", "application/iphone");
            Add("ipk", "application/vnd.webos.ipk");
            Add("cab", "application/vnd.cab-com-archive");
            Add("ipa", "application/iphone-package-archive");
            Add("deb", "application/x-debian-package-archive");
            Add("xap", "application/x-silverlight-app");
            Add("sisx", "application/vnd.symbian.epoc/x-sisx-app");
        }

        public static string GetHttpMimeType(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "application/octet-stream";
            }
            text = text.ToLower();
            string result;
            if (!_httpMimeTypes.TryGetValue(text, out result))
            {
                return "application/octet-stream";
            }
            return result;
        }

        public static void Add(string key, string value)
        {
            key = key.Trim();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (key[0] == '.')
            {
                key = key.Substring(1);
            }
            key = key.ToLower();
            _httpMimeTypes[key] = value;
        }


    }

   
}
