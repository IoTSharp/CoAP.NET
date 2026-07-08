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
    /// 标记需要宿主授权策略处理的 CoAP resource 或 action。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class CoapAuthorizeAttribute : Attribute
    {
        /// <summary>
        /// 创建默认授权 metadata。
        /// </summary>
        public CoapAuthorizeAttribute()
        {
        }

        /// <summary>
        /// 创建带策略名的授权 metadata。
        /// </summary>
        /// <param name="policy">宿主应用理解的授权策略名。</param>
        public CoapAuthorizeAttribute(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
            {
                throw new ArgumentException("CoAP authorization policy cannot be empty.", nameof(policy));
            }

            Policy = policy;
        }

        /// <summary>
        /// 获取宿主应用理解的授权策略名；为空表示使用宿主默认策略。
        /// </summary>
        public string Policy { get; }
    }

    /// <summary>
    /// 标记跳过 CoAP.NET 授权 hook 的 resource 或 action。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class CoapAllowAnonymousAttribute : Attribute
    {
    }

    /// <summary>
    /// 定义宿主实现的 CoAP 授权 hook。
    /// </summary>
    public interface ICoapAuthorizationHook
    {
        /// <summary>
        /// 根据 endpoint metadata 与请求上下文判断是否允许调用。
        /// </summary>
        /// <param name="context">授权调用上下文。</param>
        /// <returns>授权结果。</returns>
        ValueTask<CoapAuthorizationResult> AuthorizeAsync(CoapAuthorizationContext context);
    }

    /// <summary>
    /// CoAP 授权 hook 的调用上下文。
    /// </summary>
    public sealed class CoapAuthorizationContext
    {
        /// <summary>
        /// 创建授权调用上下文。
        /// </summary>
        /// <param name="invocationContext">当前 action 调用上下文。</param>
        /// <param name="requirements">endpoint 上声明的授权 metadata。</param>
        public CoapAuthorizationContext(
            CoapActionInvocationContext invocationContext,
            IReadOnlyList<CoapAuthorizeAttribute> requirements)
        {
            InvocationContext = invocationContext ?? throw new ArgumentNullException(nameof(invocationContext));
            Requirements = requirements ?? Array.Empty<CoapAuthorizeAttribute>();
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
        /// 获取 endpoint 上声明的授权 metadata。
        /// </summary>
        public IReadOnlyList<CoapAuthorizeAttribute> Requirements { get; }

        /// <summary>
        /// 获取 hook、filter 与 action 可共享的请求级上下文项。
        /// </summary>
        public IDictionary<object, object> Items => InvocationContext.Items;
    }

    /// <summary>
    /// 表示 CoAP 授权 hook 的处理结果。
    /// </summary>
    public sealed class CoapAuthorizationResult
    {
        private static readonly CoapAuthorizationResult SuccessResult =
            new CoapAuthorizationResult(true, null);

        private CoapAuthorizationResult(bool succeeded, ICoapResult failureResult)
        {
            Succeeded = succeeded;
            FailureResult = failureResult;
        }

        /// <summary>
        /// 获取授权是否通过。
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 获取授权失败时要写回客户端的响应。
        /// </summary>
        public ICoapResult FailureResult { get; }

        /// <summary>
        /// 创建授权通过结果。
        /// </summary>
        /// <returns>授权通过结果。</returns>
        public static CoapAuthorizationResult Success()
        {
            return SuccessResult;
        }

        /// <summary>
        /// 创建授权失败结果。
        /// </summary>
        /// <param name="failureResult">要写回客户端的失败响应。</param>
        /// <returns>授权失败结果。</returns>
        public static CoapAuthorizationResult Fail(ICoapResult failureResult)
        {
            if (failureResult == null)
            {
                throw new ArgumentNullException(nameof(failureResult));
            }

            return new CoapAuthorizationResult(false, failureResult);
        }

        /// <summary>
        /// 创建文本授权失败结果。
        /// </summary>
        /// <param name="statusCode">CoAP 响应状态码。</param>
        /// <param name="message">响应文本。</param>
        /// <returns>授权失败结果。</returns>
        public static CoapAuthorizationResult Fail(StatusCode statusCode, string message)
        {
            return Fail(CoapRouteResult.Text(statusCode, message));
        }
    }
}
