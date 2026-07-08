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
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Describes a routable CoAP endpoint.
    /// </summary>
    public sealed class CoapEndpoint
    {
        private readonly CoapRouteHandler _handler;

        /// <summary>
        /// Creates a CoAP endpoint from a route template.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The endpoint handler.</param>
        public CoapEndpoint(Method method, string template, CoapRouteHandler handler)
            : this(method, CoapRoutePattern.Parse(template), handler, null, null)
        {
        }

        /// <summary>
        /// Creates a CoAP endpoint from a route template.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The endpoint handler.</param>
        /// <param name="metadata">Endpoint metadata in declaration order.</param>
        /// <param name="displayName">A diagnostic endpoint name.</param>
        public CoapEndpoint(
            Method method,
            string template,
            CoapRouteHandler handler,
            IEnumerable<object> metadata,
            string displayName = null)
            : this(method, CoapRoutePattern.Parse(template), handler, metadata, displayName)
        {
        }

        /// <summary>
        /// Creates a CoAP endpoint from a parsed route pattern.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="routePattern">The parsed route pattern.</param>
        /// <param name="handler">The endpoint handler.</param>
        public CoapEndpoint(Method method, CoapRoutePattern routePattern, CoapRouteHandler handler)
            : this(method, routePattern, handler, null, null)
        {
        }

        /// <summary>
        /// Creates a CoAP endpoint from a parsed route pattern.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="routePattern">The parsed route pattern.</param>
        /// <param name="handler">The endpoint handler.</param>
        /// <param name="metadata">Endpoint metadata in declaration order.</param>
        /// <param name="displayName">A diagnostic endpoint name.</param>
        public CoapEndpoint(
            Method method,
            CoapRoutePattern routePattern,
            CoapRouteHandler handler,
            IEnumerable<object> metadata,
            string displayName = null)
        {
            Method = method;
            RoutePattern = routePattern ?? throw new ArgumentNullException(nameof(routePattern));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Metadata = metadata == null
                ? CoapEndpointMetadataCollection.Empty
                : new CoapEndpointMetadataCollection(metadata);
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? method + " " + routePattern.Template
                : displayName;
        }

        /// <summary>
        /// Gets the CoAP request method matched by this endpoint.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Gets the parsed route pattern.
        /// </summary>
        public CoapRoutePattern RoutePattern { get; }

        /// <summary>
        /// Gets endpoint metadata used by discovery, matching, filters, or host policies.
        /// </summary>
        public CoapEndpointMetadataCollection Metadata { get; }

        /// <summary>
        /// Gets a diagnostic name for this endpoint.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the first literal segment used as the root resource.
        /// </summary>
        public string RootSegment => RoutePattern.RootSegment;

        internal ValueTask<CoapRouteResult> InvokeAsync(CoapRouteContext context)
        {
            return _handler(context);
        }
    }
}
