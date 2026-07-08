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
    /// Default endpoint matcher for CoAP resource routing.
    /// </summary>
    public sealed class CoapEndpointMatcher : ICoapEndpointMatcher
    {
        private readonly ICoapEndpointDataSource _dataSource;

        /// <summary>
        /// Creates an endpoint matcher.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        public CoapEndpointMatcher(ICoapEndpointDataSource dataSource)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        /// <inheritdoc />
        public bool TryMatch(CoapEndpointMatchContext context, out CoapEndpointMatch match)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            foreach (var endpoint in _dataSource.Endpoints)
            {
                if (endpoint.Method != context.Method)
                {
                    continue;
                }

                if (!endpoint.RoutePattern.TryMatch(context.PathSegments, out var routeValues))
                {
                    continue;
                }

                if (!EndpointAcceptsContentFormat(endpoint, context.ContentFormat))
                {
                    continue;
                }

                if (!EndpointProducesAcceptedContent(endpoint, context.Accept))
                {
                    continue;
                }

                match = new CoapEndpointMatch(endpoint, routeValues);
                return true;
            }

            match = null;
            return false;
        }

        internal static IReadOnlyList<CoapEndpoint> GetPrefixMatches(
            ICoapEndpointDataSource dataSource,
            IReadOnlyList<string> pathSegments)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            if (pathSegments == null)
            {
                throw new ArgumentNullException(nameof(pathSegments));
            }

            var matches = new List<CoapEndpoint>();
            foreach (var endpoint in dataSource.Endpoints)
            {
                if (endpoint.RoutePattern.IsPrefix(pathSegments))
                {
                    matches.Add(endpoint);
                }
            }

            return matches.Count == 0 ? Array.Empty<CoapEndpoint>() : matches.ToArray();
        }

        internal static bool EndpointAcceptsContentFormat(CoapEndpoint endpoint, int contentFormat)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            var consumes = endpoint.Metadata.GetMetadata<CoapConsumesAttribute>();
            return consumes == null || ContainsMediaType(consumes.ContentFormats, contentFormat);
        }

        internal static bool EndpointProducesAcceptedContent(CoapEndpoint endpoint, int accept)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (accept == MediaType.Undefined || accept == MediaType.Any)
            {
                return true;
            }

            var produces = endpoint.Metadata.GetMetadata<CoapProducesAttribute>();
            return produces == null || ContainsMediaType(produces.ContentFormats, accept);
        }

        private static bool ContainsMediaType(IReadOnlyList<int> contentFormats, int contentFormat)
        {
            if (contentFormats == null || contentFormats.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < contentFormats.Count; i++)
            {
                if (contentFormats[i] == MediaType.Any || contentFormats[i] == contentFormat)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
