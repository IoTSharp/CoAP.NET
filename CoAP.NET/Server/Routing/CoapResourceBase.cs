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
    /// Base class for CoAP MVC-style resource classes that need access to the
    /// current route context without declaring it on every action method.
    /// </summary>
    public abstract class CoapResourceBase
    {
        private CoapRouteContext _context;

        /// <summary>
        /// Gets the current matched CoAP route context.
        /// </summary>
        public CoapRouteContext Context
        {
            get
            {
                if (_context == null)
                {
                    throw new InvalidOperationException("No CoAP route context is available for the current resource invocation.");
                }

                return _context;
            }
        }

        /// <summary>
        /// Gets the CoAP request method from the current context.
        /// </summary>
        public Method Method => Context.Method;

        /// <summary>
        /// Gets the URI path segments from the current context.
        /// </summary>
        public IReadOnlyList<string> PathSegments => Context.PathSegments;

        /// <summary>
        /// Gets values extracted from route template parameters.
        /// </summary>
        public IReadOnlyDictionary<string, string> RouteValues => Context.RouteValues;

        /// <summary>
        /// Gets URI query options from the current request.
        /// </summary>
        public IReadOnlyList<string> Queries => Context.Queries;

        /// <summary>
        /// Gets the request payload without forcing a string conversion.
        /// </summary>
        public ReadOnlyMemory<byte> Payload => Context.Payload;

        /// <summary>
        /// Gets the request Content-Format option value.
        /// </summary>
        public int ContentFormat => Context.ContentFormat;

        /// <summary>
        /// Gets the request Accept option value.
        /// </summary>
        public int Accept => Context.Accept;

        /// <summary>
        /// Gets the Observe option value, or null when the request is not an observe operation.
        /// </summary>
        public int? Observe => Context.Observe;

        /// <summary>
        /// Gets request options visible to the routing layer.
        /// </summary>
        public IReadOnlyList<Option> Options => Context.Options;

        /// <summary>
        /// Gets ETag option values carried by the request.
        /// </summary>
        public IReadOnlyList<byte[]> ETags => Context.ETags;

        /// <summary>
        /// Gets the request Block1 option, or null when absent.
        /// </summary>
        public BlockOption Block1 => Context.Block1;

        /// <summary>
        /// Gets the request Block2 option, or null when absent.
        /// </summary>
        public BlockOption Block2 => Context.Block2;

        /// <summary>
        /// Gets the scoped service provider for the current request, if available.
        /// </summary>
        public IServiceProvider RequestServices => Context.RequestServices;

        internal async ValueTask<CoapRouteResult> InvokeWithContextAsync(
            CoapRouteContext context,
            Func<ValueTask<CoapRouteResult>> action)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var previousContext = _context;
            _context = context;
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                _context = previousContext;
            }
        }
    }
}
