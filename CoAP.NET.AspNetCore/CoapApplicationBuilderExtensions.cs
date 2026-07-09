/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using CoAP.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// ASP.NET Core adapters for mapping CoAP.NET resources from Startup.Configure.
    /// </summary>
    public static class CoapApplicationBuilderExtensions
    {
        /// <summary>
        /// Maps registered CoAP resource endpoints to the hosted CoAP server.
        /// </summary>
        /// <param name="app">The ASP.NET Core application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder MapCoapResources(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            app.ApplicationServices.GetRequiredService<CoapResourceEndpointMapper>().Map();
            return app;
        }

        /// <summary>
        /// Enables the hosted CoAP server integration for an ASP.NET Core application.
        /// </summary>
        /// <param name="app">The ASP.NET Core application builder.</param>
        /// <returns>The application builder.</returns>
        public static IApplicationBuilder UseCoapServer(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            var listenOptions = app.ApplicationServices.GetService<CoapServerListenOptions>();
            if (listenOptions != null && !listenOptions.HasEnabledTransport)
            {
                return app;
            }

            app.ApplicationServices.GetRequiredService<CoapResourceEndpointMapper>().Map();
            return app;
        }
    }
}
