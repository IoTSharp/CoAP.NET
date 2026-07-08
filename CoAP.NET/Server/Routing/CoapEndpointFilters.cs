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
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// 表示 CoAP endpoint filter 链中的下一步。
    /// </summary>
    /// <param name="context">当前 CoAP action 调用上下文。</param>
    /// <returns>endpoint 或后续 filter 生成的 CoAP 响应。</returns>
    public delegate ValueTask<ICoapResult> CoapEndpointFilterDelegate(CoapActionInvocationContext context);

    /// <summary>
    /// 定义 CoAP endpoint 调用前后的框架级过滤器。
    /// </summary>
    public interface ICoapEndpointFilter
    {
        /// <summary>
        /// 执行 filter，可选择调用 <paramref name="next"/> 继续管线，也可直接返回响应短路请求。
        /// </summary>
        /// <param name="context">当前 CoAP action 调用上下文。</param>
        /// <param name="next">filter 链中的下一步。</param>
        /// <returns>要写回客户端的 CoAP 响应。</returns>
        ValueTask<ICoapResult> InvokeAsync(
            CoapActionInvocationContext context,
            CoapEndpointFilterDelegate next);
    }

    /// <summary>
    /// 允许 resource 或 action 通过 attribute 声明 endpoint filter。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class CoapEndpointFilterAttribute : Attribute, ICoapEndpointFilter
    {
        /// <inheritdoc />
        public abstract ValueTask<ICoapResult> InvokeAsync(
            CoapActionInvocationContext context,
            CoapEndpointFilterDelegate next);
    }
}
