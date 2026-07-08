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
using System.Linq;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Immutable endpoint data source backed by an in-memory endpoint list.
    /// </summary>
    public sealed class CoapEndpointDataSource : ICoapEndpointDataSource
    {
        /// <summary>
        /// Creates an endpoint data source.
        /// </summary>
        /// <param name="endpoints">The endpoints to expose.</param>
        public CoapEndpointDataSource(IEnumerable<CoapEndpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var endpointArray = endpoints.ToArray();
            for (var i = 0; i < endpointArray.Length; i++)
            {
                if (endpointArray[i] == null)
                {
                    throw new ArgumentException("Endpoint collection cannot contain null entries.", nameof(endpoints));
                }
            }

            Endpoints = endpointArray;
        }

        /// <inheritdoc />
        public IReadOnlyList<CoapEndpoint> Endpoints { get; }

        /// <summary>
        /// Creates an endpoint data source from legacy route descriptors.
        /// </summary>
        /// <param name="routes">The route descriptors.</param>
        /// <returns>An endpoint data source.</returns>
        public static CoapEndpointDataSource FromRoutes(IEnumerable<CoapRoute> routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            return new CoapEndpointDataSource(routes.Select(route =>
            {
                if (route == null)
                {
                    throw new ArgumentException("Route collection cannot contain null entries.", nameof(routes));
                }

                return route.Endpoint;
            }));
        }
    }
}
