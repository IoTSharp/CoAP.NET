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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Context used while invoking a matched CoAP endpoint.
    /// </summary>
    public sealed class CoapActionInvocationContext
    {
        /// <summary>
        /// Creates an action invocation context.
        /// </summary>
        /// <param name="routeContext">The matched route context.</param>
        /// <param name="requestServices">The scoped request services, if available.</param>
        public CoapActionInvocationContext(
            CoapRouteContext routeContext,
            IServiceProvider requestServices)
        {
            RouteContext = routeContext ?? throw new ArgumentNullException(nameof(routeContext));
            RequestServices = requestServices;
        }

        /// <summary>
        /// Gets the selected endpoint.
        /// </summary>
        public CoapEndpoint Endpoint => RouteContext.Endpoint;

        /// <summary>
        /// Gets the matched route context.
        /// </summary>
        public CoapRouteContext RouteContext { get; }

        /// <summary>
        /// Gets the scoped request services, if available.
        /// </summary>
        public IServiceProvider RequestServices { get; }
    }
}
