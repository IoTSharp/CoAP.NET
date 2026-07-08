/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// 定义宿主实现的 CoAP 请求上下文注入 hook。
    /// </summary>
    public interface ICoapRequestContextHook
    {
        /// <summary>
        /// 在授权与 endpoint filter 执行前补充请求级上下文。
        /// </summary>
        /// <param name="context">上下文注入调用上下文。</param>
        /// <returns>异步操作。</returns>
        ValueTask EnrichAsync(CoapRequestContextHookContext context);
    }

    /// <summary>
    /// CoAP 请求上下文注入 hook 的调用上下文。
    /// </summary>
    public sealed class CoapRequestContextHookContext
    {
        /// <summary>
        /// 创建上下文注入调用上下文。
        /// </summary>
        /// <param name="invocationContext">当前 action 调用上下文。</param>
        public CoapRequestContextHookContext(CoapActionInvocationContext invocationContext)
        {
            InvocationContext = invocationContext ?? throw new ArgumentNullException(nameof(invocationContext));
        }

        /// <summary>
        /// 获取当前 action 调用上下文。
        /// </summary>
        public CoapActionInvocationContext InvocationContext { get; }

        /// <summary>
        /// 获取当前 endpoint。
        /// </summary>
        public CoapEndpoint Endpoint => InvocationContext.Endpoint;

        /// <summary>
        /// 获取当前路由上下文。
        /// </summary>
        public CoapRouteContext RouteContext => InvocationContext.RouteContext;

        /// <summary>
        /// 获取当前请求服务容器。
        /// </summary>
        public IServiceProvider RequestServices => InvocationContext.RequestServices;

        /// <summary>
        /// 获取 hook、filter 与 action 可共享的请求级上下文项。
        /// </summary>
        public IDictionary<object, object> Items => InvocationContext.Items;
    }
}
