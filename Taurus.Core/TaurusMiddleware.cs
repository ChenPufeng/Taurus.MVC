﻿using CYQ.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Taurus.Core;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// 为支持Asp.net core 存在的文件
    /// </summary>
    public class TaurusMiddleware
    {
        private readonly RequestDelegate next;

        public TaurusMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                if (context.Request.Path.Value.IndexOf("/App_Data/", StringComparison.OrdinalIgnoreCase) > -1)//兼容受保护的目录
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("403 Forbidden");
                }
                else
                {
                    System.Web.HttpApplication.Instance.ExecuteEventHandler();
                    if (context.Response.HasStarted)  // || Body是只写流  (context.Response.Body != null && context.Response.Body.CanRead
                    {
                        await context.Response.WriteAsync("");
                    }
                    //处理信息
                    else
                    {
                        await next(context);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLogToTxt(ex);
            }
        }
    }
    public static class TaurusExtensions
    {
        public static IApplicationBuilder UseTaurusMvc(this IApplicationBuilder builder, IHostingEnvironment env)
        {
            AppConfig.WebRootPath = env.WebRootPath;//设置根目录地址，ASPNETCore的根目录和其它应用不一样。
            //执行一次，用于注册事件
            UrlRewrite url = new UrlRewrite();
            url.Init(System.Web.HttpApplication.Instance);
            return builder.UseMiddleware<TaurusMiddleware>();
        }
    }
}
