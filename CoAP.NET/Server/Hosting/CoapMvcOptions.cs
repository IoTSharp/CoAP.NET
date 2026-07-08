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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace CoAP.Server.Hosting
{
    /// <summary>
    /// Configures CoAP resource discovery and endpoint registration.
    /// </summary>
    public sealed class CoapMvcOptions
    {
        private bool _reflectionResourceDiscoveryEnabled;

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
        /// Gets endpoint factories registered by source generators or compatibility adapters.
        /// </summary>
        public IList<Func<IServiceProvider, IEnumerable<CoapEndpoint>>> EndpointFactories { get; } =
            new List<Func<IServiceProvider, IEnumerable<CoapEndpoint>>>();

        /// <summary>
        /// Adds a plugin or external assembly to the resource application parts.
        /// </summary>
        /// <param name="assembly">The assembly that contains CoAP resource classes.</param>
        /// <returns>The current options instance.</returns>
        [RequiresUnreferencedCode("Application part discovery scans resource assemblies with reflection. Native AOT hosts should use generated endpoint factories.")]
        [RequiresDynamicCode("Application part discovery invokes runtime-discovered resource methods. Native AOT hosts should use generated endpoint factories.")]
        public CoapMvcOptions AddApplicationPart(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            ApplicationParts.Add(assembly);
            AddReflectionResourceDiscovery();
            return this;
        }

        /// <summary>
        /// Adds the assembly that contains the specified marker type.
        /// </summary>
        /// <typeparam name="TMarker">A type from the assembly to add.</typeparam>
        /// <returns>The current options instance.</returns>
        [RequiresUnreferencedCode("Application part discovery scans resource assemblies with reflection. Native AOT hosts should use generated endpoint factories.")]
        [RequiresDynamicCode("Application part discovery invokes runtime-discovered resource methods. Native AOT hosts should use generated endpoint factories.")]
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

        /// <summary>
        /// Adds an endpoint factory, typically emitted by a source generator.
        /// </summary>
        /// <param name="endpointFactory">Factory that creates endpoint descriptors.</param>
        /// <returns>The current options instance.</returns>
        public CoapMvcOptions AddEndpointFactory(Func<IServiceProvider, IEnumerable<CoapEndpoint>> endpointFactory)
        {
            if (endpointFactory == null)
            {
                throw new ArgumentNullException(nameof(endpointFactory));
            }

            EndpointFactories.Add(endpointFactory);
            return this;
        }

        /// <summary>
        /// Enables reflection-based resource discovery for non-AOT hosts.
        /// </summary>
        /// <returns>The current options instance.</returns>
        [RequiresUnreferencedCode("Reflection-based CoAP resource discovery is not trim-safe. Native AOT hosts should use generated endpoint factories.")]
        [RequiresDynamicCode("Reflection-based CoAP resource discovery invokes runtime-discovered methods. Native AOT hosts should use generated endpoint factories.")]
        public CoapMvcOptions AddReflectionResourceDiscovery()
        {
            if (!_reflectionResourceDiscoveryEnabled)
            {
                _reflectionResourceDiscoveryEnabled = true;
                EndpointFactories.Add(new ReflectionResourceEndpointFactoryMarker(this).BuildEndpoints);
            }

            return this;
        }

        internal IReadOnlyList<CoapEndpoint> BuildEndpoints(IServiceProvider serviceProvider)
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

            for (var i = 0; i < EndpointFactories.Count; i++)
            {
                var endpointFactory = EndpointFactories[i];
                if (endpointFactory == null)
                {
                    throw new InvalidOperationException("CoAP endpoint factory registration cannot contain null entries.");
                }

                var factoryEndpoints = endpointFactory(serviceProvider);
                if (factoryEndpoints == null)
                {
                    continue;
                }

                foreach (var endpoint in factoryEndpoints)
                {
                    if (endpoint == null)
                    {
                        throw new InvalidOperationException("CoAP endpoint factory cannot return null entries.");
                    }

                    endpoints.Add(endpoint);
                }
            }

            return endpoints.Count == 0 ? Array.Empty<CoapEndpoint>() : endpoints.ToArray();
        }

        private sealed class ReflectionResourceEndpointFactoryMarker
        {
            private readonly CoapMvcOptions _options;

            public ReflectionResourceEndpointFactoryMarker(CoapMvcOptions options)
            {
                _options = options ?? throw new ArgumentNullException(nameof(options));
            }

            [RequiresUnreferencedCode("Reflection-based CoAP resource discovery is not trim-safe. Native AOT hosts should use generated endpoint factories.")]
            [RequiresDynamicCode("Reflection-based CoAP resource discovery invokes runtime-discovered methods. Native AOT hosts should use generated endpoint factories.")]
            public IEnumerable<CoapEndpoint> BuildEndpoints(IServiceProvider serviceProvider)
            {
                return CoapResourceEndpointBuilder.Build(_options);
            }
        }
    }
}
