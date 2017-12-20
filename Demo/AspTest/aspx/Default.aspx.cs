using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class _Default : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        this.ipTxt.Value = GetIp();
    }

    public  string GetIp()
    {
        string result = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        result += "," + HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

        result += "," + HttpContext.Current.Request.UserHostAddress;

        if (string.IsNullOrEmpty(result) || !IsIPv4(result))
        {
            return "127.0.0.1";
        }

        return result;
    }

    public  bool IsIPv4(string ip)
    {
        return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
    }
}