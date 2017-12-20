![](https://img.shields.io/badge/platform-dotnet-red.svg) ![](https://img.shields.io/badge/language-CSharp-orange.svg) 
[![Support](https://img.shields.io/badge/support-NetCore-blue.svg?style=flat)](https://www.microsoft.com/net/core) 
[![Weibo](https://img.shields.io/badge/博客园-@鱼东东-yellow.svg?style=flat)](http://www.cnblogs.com/yudongdong) 
[![GitHub stars](https://img.shields.io/github/stars/yuzd/OwinDog.svg)](https://github.com/yuzd/OwinDog/stargazers)


# 什么是 OWIN ？
　　.OWIN 的全称是 "Open Web Interface for .NET"， OWIN 在 .NET Web 服务器和 .NET Web 应用之间定义了一套标准的接口，
    其目的是为了实现服务器与应用之间的解耦， 鼓励为 .NET Web 应用开发简单模块。


# OwinDog 是一款支持OWIN标准的WEB应用的高性能的HTTP服务器，有如下特点：

1，跨平台：支持windows、linux等常用操作系统(后者由mono支持)；

2，超轻量：功能单一而明确：除了静态文件由自身处理外，其它的应用逻辑直接交给用户处理；

3，高性能：底层基于 libuv 开发，是完全的异步、非阻塞、事件驱动模型，上层代码也经过了高度优化；libuv是NodeJs的基础库，libuv 是一个高性能事件驱动的程序库，封装了 Windows 和 Unix 平台一些底层特性，为开发者提供了统一的 API，libuv 采用了异步 (asynchronous), 事件驱动 (event-driven)的编程风格, 其主要任务是为开人员提供了一套事件循环和基于I/O(或其他活动)通知的回调函数, libuv 提供了一套核心的工具集, 例如定时器, 非阻塞网络编程的支持, 异步访问文件系统, 子进程以及其他功能，关于libuv的更多内容推荐参考电子书 http://www.nowx.org/uvbook/ 。


# 测试访问aspx的demo
[aspx demo](https://files.cnblogs.com/files/yudongdong/%E6%B5%8B%E8%AF%95aspx.zip)


欢迎测试，如果你有什么问题，请提交Issue或者加入QQ群433685124
