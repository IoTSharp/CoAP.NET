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

using CoAP.Server.Resources;
using CoAP.Server.Routing;
using System;
using System.Collections.Generic;

namespace CoAP.Server.Hosting
{
    internal sealed class CoapResourceEndpointMapper
    {
        private readonly IServer _server;
        private readonly ICoapEndpointDataSource _dataSource;
        private readonly ICoapEndpointMatcher _matcher;
        private readonly CoapRequestDispatcher _dispatcher;
        private readonly CoapRouteObserveRegistry _observeRegistry;
        private readonly object _sync = new object();
        private IReadOnlyList<IResource> _mappedResources;

        /// <summary>
        /// Creates a mapper for attaching resource endpoints to a CoAP server.
        /// </summary>
        /// <param name="server">The CoAP server that owns the resource tree.</param>
        /// <param name="dataSource">The registered endpoint data source.</param>
        /// <param name="matcher">The endpoint matcher used by generated route resources.</param>
        /// <param name="dispatcher">The request dispatcher used by generated route resources.</param>
        /// <param name="observeRegistry">The registry used to track route Observe relations.</param>
        public CoapResourceEndpointMapper(
            IServer server,
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher,
            CoapRequestDispatcher dispatcher,
            CoapRouteObserveRegistry observeRegistry)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _observeRegistry = observeRegistry ?? throw new ArgumentNullException(nameof(observeRegistry));
        }

        /// <summary>
        /// Adds registered CoAP route resources to the server resource tree once.
        /// </summary>
        /// <returns>The mapped root resources.</returns>
        public IReadOnlyList<IResource> Map()
        {
            lock (_sync)
            {
                if (_mappedResources != null)
                {
                    return _mappedResources;
                }

                var resources = CoapRouteEndpoint.Create(_dataSource, _matcher, _dispatcher, _observeRegistry);
                foreach (var resource in resources)
                {
                    _server.Add(resource);
                }

                _mappedResources = resources;
                return _mappedResources;
            }
        }
    }
}
