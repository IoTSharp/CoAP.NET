/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using CoAP.Net;
using System;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Context used while writing a CoAP action result.
    /// </summary>
    public sealed class CoapResultExecutionContext
    {
        /// <summary>
        /// Creates a result execution context.
        /// </summary>
        /// <param name="exchange">The CoAP exchange.</param>
        /// <param name="routeContext">The matched route context, if available.</param>
        /// <param name="requestServices">The scoped request services, if available.</param>
        public CoapResultExecutionContext(
            Exchange exchange,
            CoapRouteContext routeContext,
            IServiceProvider requestServices)
        {
            Exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
            RouteContext = routeContext;
            RequestServices = requestServices;
        }

        /// <summary>
        /// Gets the CoAP exchange.
        /// </summary>
        public Exchange Exchange { get; }

        /// <summary>
        /// Gets the matched route context, if available.
        /// </summary>
        public CoapRouteContext RouteContext { get; }

        /// <summary>
        /// Gets the scoped request services, if available.
        /// </summary>
        public IServiceProvider RequestServices { get; }
    }
}
