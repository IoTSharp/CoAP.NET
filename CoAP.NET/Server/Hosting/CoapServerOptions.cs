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

using CoAP.Net;
using System;
using System.Collections.Generic;
using System.Net;

namespace CoAP.Server.Hosting
{
    /// <summary>
    /// Configures a hosted <see cref="CoapServer"/> instance.
    /// </summary>
    public sealed class CoapServerOptions
    {
        private readonly List<Func<IServiceProvider, ICoapConfig, IEndPoint>> _endpointFactories =
            new List<Func<IServiceProvider, ICoapConfig, IEndPoint>>();

        /// <summary>
        /// Gets or sets the CoAP protocol configuration used by the hosted server.
        /// </summary>
        public ICoapConfig Config { get; set; } = CoapConfig.Default;

        internal IReadOnlyList<Func<IServiceProvider, ICoapConfig, IEndPoint>> EndpointFactories =>
            _endpointFactories;

        /// <summary>
        /// Adds a UDP listener bound to any local address.
        /// </summary>
        /// <param name="port">The UDP port to bind.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenAnyIP(int port)
        {
            return Listen(IPAddress.Any, port);
        }

        /// <summary>
        /// Adds a UDP listener bound to the loopback address.
        /// </summary>
        /// <param name="port">The UDP port to bind.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenLocalhost(int port)
        {
            return Listen(IPAddress.Loopback, port);
        }

        /// <summary>
        /// Adds a UDP listener bound to the specified address and port.
        /// </summary>
        /// <param name="address">The local address.</param>
        /// <param name="port">The UDP port to bind.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions Listen(IPAddress address, int port)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            ValidatePort(port);
            return Listen(new IPEndPoint(address, port));
        }

        /// <summary>
        /// Adds a UDP listener bound to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The local UDP endpoint.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions Listen(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            ValidatePort(endpoint.Port);
            return UseEndPoint((_, config) => new CoAPEndPoint(endpoint, config));
        }

        /// <summary>
        /// Adds an already-created CoAP endpoint to the hosted server.
        /// </summary>
        /// <param name="endpoint">The endpoint to attach.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions UseEndPoint(IEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return UseEndPoint((_, _) => endpoint);
        }

        /// <summary>
        /// Adds a CoAP endpoint created from the active CoAP configuration.
        /// </summary>
        /// <param name="endpointFactory">Creates the endpoint.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions UseEndPoint(Func<ICoapConfig, IEndPoint> endpointFactory)
        {
            if (endpointFactory == null)
            {
                throw new ArgumentNullException(nameof(endpointFactory));
            }

            return UseEndPoint((_, config) => endpointFactory(config));
        }

        /// <summary>
        /// Adds a CoAP endpoint created from the service provider and active configuration.
        /// </summary>
        /// <param name="endpointFactory">Creates the endpoint.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions UseEndPoint(Func<IServiceProvider, ICoapConfig, IEndPoint> endpointFactory)
        {
            if (endpointFactory == null)
            {
                throw new ArgumentNullException(nameof(endpointFactory));
            }

            _endpointFactories.Add(endpointFactory);
            return this;
        }

        private static void ValidatePort(int port)
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 0 and 65535.");
            }
        }
    }
}
