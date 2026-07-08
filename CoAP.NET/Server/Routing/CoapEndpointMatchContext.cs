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
    /// Input used to select a CoAP endpoint for a request.
    /// </summary>
    public sealed class CoapEndpointMatchContext
    {
        /// <summary>
        /// Creates endpoint matching input.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="pathSegments">The URI path segments.</param>
        /// <param name="contentFormat">The request Content-Format option value.</param>
        /// <param name="accept">The request Accept option value.</param>
        /// <param name="observe">The request Observe option value, or null when not present.</param>
        public CoapEndpointMatchContext(
            Method method,
            IReadOnlyList<string> pathSegments,
            int contentFormat,
            int accept,
            int? observe)
        {
            Method = method;
            PathSegments = pathSegments ?? throw new ArgumentNullException(nameof(pathSegments));
            ContentFormat = contentFormat;
            Accept = accept;
            Observe = observe;
        }

        /// <summary>
        /// Gets the CoAP request method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Gets the URI path segments.
        /// </summary>
        public IReadOnlyList<string> PathSegments { get; }

        /// <summary>
        /// Gets the request Content-Format option value.
        /// </summary>
        public int ContentFormat { get; }

        /// <summary>
        /// Gets the request Accept option value.
        /// </summary>
        public int Accept { get; }

        /// <summary>
        /// Gets the Observe option value, or null when the request is not an observe operation.
        /// </summary>
        public int? Observe { get; }
    }
}
