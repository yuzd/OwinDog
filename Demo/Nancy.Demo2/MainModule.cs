using System;
using Nancy;



namespace Nancy.Demo2
{

    public class MainModule : NancyModule
    {

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainModule()
        {

            //在构造函数中进行路由配置

            Get["/"] = IndexPage;
            Get["/test/{abc}"] = ToTest;
            Get["/test"] = _ => "this test.....";
            Get["/get"] = _ => Request.Session["kkkk"] == null ? "nonoono" : "okokok";
            Get["/set"] = _ => { Request.Session["kkkk"] = "okkkk"; return "set ok."; };

        }



        /// <summary>
        /// 主页的实现方法
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private dynamic IndexPage(dynamic d)
        {
            //显示cshtml页
            return View["Home/Index"];
        }





        private dynamic ToTest(dynamic d)
        {
            return View["Test", d];
        }



    }





}