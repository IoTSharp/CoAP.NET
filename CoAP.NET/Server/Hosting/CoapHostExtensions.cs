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

using CoAP.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Maps CoAP resource endpoints to the hosted CoAP server.
    /// </summary>
    public static class CoapHostExtensions
    {
        /// <summary>
        /// Maps registered CoAP resource endpoints to the hosted server resource tree.
        /// </summary>
        /// <typeparam name="THost">The host type.</typeparam>
        /// <param name="host">The application host.</param>
        /// <returns>The host.</returns>
        public static THost MapCoapResources<THost>(this THost host)
            where THost : IHost
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            host.Services.GetRequiredService<CoapResourceEndpointMapper>().Map();
            return host;
        }
    }
}
