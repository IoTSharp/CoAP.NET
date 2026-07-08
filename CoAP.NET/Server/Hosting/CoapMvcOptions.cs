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

using CoAP.Server.Routing;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CoAP.Server.Hosting
{
    /// <summary>
    /// Configures CoAP resource discovery and endpoint registration.
    /// </summary>
    public sealed class CoapMvcOptions
    {
        /// <summary>
        /// Gets plugin or external assemblies that should participate in future resource discovery.
        /// </summary>
        public IList<Assembly> ApplicationParts { get; } = new List<Assembly>();

        /// <summary>
        /// Gets endpoint descriptors registered directly with the resource hosting layer.
        /// </summary>
        public IList<CoapEndpoint> Endpoints { get; } = new List<CoapEndpoint>();

        /// <summary>
        /// Gets low-level route descriptors registered for compatibility or tests.
        /// </summary>
        public IList<CoapRoute> Routes { get; } = new List<CoapRoute>();

        /// <summary>
        /// Adds a plugin or external assembly to the resource application parts.
        /// </summary>
        /// <param name="assembly">The assembly that contains CoAP resource classes.</param>
        /// <returns>The current options instance.</returns>
        public CoapMvcOptions AddApplicationPart(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            ApplicationParts.Add(assembly);
            return this;
        }

        /// <summary>
        /// Adds the assembly that contains the specified marker type.
        /// </summary>
        /// <typeparam name="TMarker">A type from the assembly to add.</typeparam>
        /// <returns>The current options instance.</returns>
        public CoapMvcOptions AddApplicationPart<TMarker>()
        {
            return AddApplicationPart(typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Adds a CoAP endpoint descriptor.
        /// </summary>
        /// <param name="endpoint">The endpoint descriptor.</param>
        /// <returns>The current options instance.</returns>
        public CoapMvcOptions AddEndpoint(CoapEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            Endpoints.Add(endpoint);
            return this;
        }

        /// <summary>
        /// Adds a low-level route descriptor.
        /// </summary>
        /// <param name="route">The route descriptor.</param>
        /// <returns>The current options instance.</returns>
        public CoapMvcOptions AddRoute(CoapRoute route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            Routes.Add(route);
            return this;
        }

        internal IReadOnlyList<CoapEndpoint> BuildEndpoints()
        {
            var endpoints = new List<CoapEndpoint>(Endpoints.Count + Routes.Count);
            for (var i = 0; i < Endpoints.Count; i++)
            {
                var endpoint = Endpoints[i];
                if (endpoint == null)
                {
                    throw new InvalidOperationException("CoAP endpoint registration cannot contain null entries.");
                }

                endpoints.Add(endpoint);
            }

            for (var i = 0; i < Routes.Count; i++)
            {
                var route = Routes[i];
                if (route == null)
                {
                    throw new InvalidOperationException("CoAP route registration cannot contain null entries.");
                }

                endpoints.Add(route.Endpoint);
            }

            return endpoints.Count == 0 ? Array.Empty<CoapEndpoint>() : endpoints.ToArray();
        }
    }
}
