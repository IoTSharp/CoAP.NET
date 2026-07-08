/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Generic;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Result of matching a CoAP request to an endpoint.
    /// </summary>
    public sealed class CoapEndpointMatch
    {
        /// <summary>
        /// Creates an endpoint match result.
        /// </summary>
        /// <param name="endpoint">The selected endpoint.</param>
        /// <param name="routeValues">Values extracted from route parameters.</param>
        public CoapEndpointMatch(
            CoapEndpoint endpoint,
            IReadOnlyDictionary<string, string> routeValues)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            RouteValues = routeValues ?? throw new ArgumentNullException(nameof(routeValues));
        }

        /// <summary>
        /// Gets the selected endpoint.
        /// </summary>
        public CoapEndpoint Endpoint { get; }

        /// <summary>
        /// Gets values extracted from route parameters.
        /// </summary>
        public IReadOnlyDictionary<string, string> RouteValues { get; }
    }
}
