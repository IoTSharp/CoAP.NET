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

using CoAP;
using CoAP.Net;
using CoAP.Server;
using CoAP.Server.Hosting;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers CoAP.NET server hosting and resource services.
    /// </summary>
    public static class CoapServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a hosted CoAP server with default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapServer(this IServiceCollection services)
        {
            return AddCoapServer(services, null);
        }

        /// <summary>
        /// Registers a hosted CoAP server.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures CoAP server options.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapServer(
            this IServiceCollection services,
            Action<CoapServerOptions> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions<CoapServerOptions>();
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton(CreateCoapServer);
            services.TryAddSingleton<IServer>(serviceProvider => serviceProvider.GetRequiredService<CoapServer>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CoapServerHostedService>());
            return services;
        }

        /// <summary>
        /// Registers CoAP resource routing services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapResources(this IServiceCollection services)
        {
            return AddCoapResources(services, null);
        }

        /// <summary>
        /// Registers CoAP resource routing services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures resource discovery and endpoint registration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapResources(
            this IServiceCollection services,
            Action<CoapMvcOptions> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions<CoapMvcOptions>();
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton<ICoapEndpointDataSource>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<CoapMvcOptions>>().Value;
                return new CoapEndpointDataSource(options.BuildEndpoints());
            });
            services.TryAddSingleton<ICoapEndpointMatcher, CoapEndpointMatcher>();
            services.TryAddSingleton<CoapActionInvoker>();
            services.TryAddSingleton<ICoapResultExecutor, CoapResultExecutor>();
            services.TryAddSingleton<ICoapJsonPayloadBinder, CoapSystemTextJsonPayloadBinder>();
            services.TryAddSingleton<CoapRequestDispatcher>();
            services.TryAddSingleton<CoapResourceEndpointMapper>();
            return services;
        }

        /// <summary>
        /// Registers the full CoAP MVC resource stack.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapMvc(this IServiceCollection services)
        {
            return AddCoapResources(services);
        }

        /// <summary>
        /// Registers the full CoAP MVC resource stack.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures resource discovery and endpoint registration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCoapMvc(
            this IServiceCollection services,
            Action<CoapMvcOptions> configure)
        {
            return AddCoapResources(services, configure);
        }

        private static CoapServer CreateCoapServer(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetRequiredService<IOptions<CoapServerOptions>>().Value;
            var config = options.Config ?? CoapConfig.Default;
            var server = new CoapServer(config);
            foreach (var endpointFactory in options.EndpointFactories)
            {
                var endpoint = endpointFactory(serviceProvider, config);
                if (endpoint == null)
                {
                    throw new InvalidOperationException("The CoAP endpoint factory returned null.");
                }

                server.AddEndPoint(endpoint);
            }

            return server;
        }
    }
}
