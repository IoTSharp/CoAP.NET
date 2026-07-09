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

using CoAP.Channel;
using CoAP.Net;
using Microsoft.Extensions.Logging;
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
        /// Applies configuration-bound listen settings to the hosted server.
        /// </summary>
        /// <param name="listenOptions">The listen settings.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ApplyListenOptions(CoapServerListenOptions listenOptions)
        {
            if (listenOptions == null)
            {
                throw new ArgumentNullException(nameof(listenOptions));
            }

            listenOptions.Validate();
            Config = listenOptions;

            if (listenOptions.Enabled)
            {
                ListenCoap(listenOptions.GetCoapBindAddress(), listenOptions.GetCoapPort());
            }

            if (listenOptions.Dtls?.Enabled == true)
            {
                ListenCoapsPsk(
                    listenOptions.GetCoapsBindAddress(),
                    listenOptions.GetCoapsPort(),
                    listenOptions.Dtls.PskKeys,
                    listenOptions.Dtls.GetSessionIdleTimeout());
            }

            return this;
        }

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
        /// Adds a UDP <c>coap://</c> listener using the default CoAP port.
        /// </summary>
        /// <param name="bindAddress">The local bind address.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoap(string bindAddress)
        {
            return ListenCoap(bindAddress, CoapConfig.Default.DefaultPort);
        }

        /// <summary>
        /// Adds a UDP <c>coap://</c> listener.
        /// </summary>
        /// <param name="bindAddress">The local bind address.</param>
        /// <param name="port">The UDP port to bind.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoap(string bindAddress, int port)
        {
            return ListenCoap(CoapServerListenOptions.ParseBindAddress(bindAddress, nameof(bindAddress)), port);
        }

        /// <summary>
        /// Adds a UDP <c>coap://</c> listener using the default CoAP port.
        /// </summary>
        /// <param name="address">The local address.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoap(IPAddress address)
        {
            return ListenCoap(address, CoapConfig.Default.DefaultPort);
        }

        /// <summary>
        /// Adds a UDP <c>coap://</c> listener.
        /// </summary>
        /// <param name="address">The local address.</param>
        /// <param name="port">The UDP port to bind.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoap(IPAddress address, int port)
        {
            return Listen(address, port);
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
            return UseEndPoint((services, config) =>
            {
                LogListener(services, "CoAP UDP/coap", endpoint);
                return new CoAPEndPoint(endpoint, config);
            });
        }

        /// <summary>
        /// Adds a DTLS PSK <c>coaps://</c> listener using the default secure CoAP port.
        /// </summary>
        /// <param name="bindAddress">The local bind address.</param>
        /// <param name="pskKeys">PSK identity-to-key mappings.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoapsPsk(
            string bindAddress,
            IReadOnlyDictionary<string, string> pskKeys)
        {
            return ListenCoapsPsk(bindAddress, CoapConfig.Default.DefaultSecurePort, pskKeys);
        }

        /// <summary>
        /// Adds a DTLS PSK <c>coaps://</c> listener.
        /// </summary>
        /// <param name="bindAddress">The local bind address.</param>
        /// <param name="port">The UDP port to bind.</param>
        /// <param name="pskKeys">PSK identity-to-key mappings.</param>
        /// <param name="sessionIdleTimeout">The optional DTLS session idle timeout.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoapsPsk(
            string bindAddress,
            int port,
            IReadOnlyDictionary<string, string> pskKeys,
            TimeSpan? sessionIdleTimeout = null)
        {
            return ListenCoapsPsk(
                CoapServerListenOptions.ParseBindAddress(bindAddress, nameof(bindAddress)),
                port,
                pskKeys,
                sessionIdleTimeout);
        }

        /// <summary>
        /// Adds a DTLS PSK <c>coaps://</c> listener using the default secure CoAP port.
        /// </summary>
        /// <param name="address">The local address.</param>
        /// <param name="pskKeys">PSK identity-to-key mappings.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoapsPsk(
            IPAddress address,
            IReadOnlyDictionary<string, string> pskKeys)
        {
            return ListenCoapsPsk(address, CoapConfig.Default.DefaultSecurePort, pskKeys);
        }

        /// <summary>
        /// Adds a DTLS PSK <c>coaps://</c> listener.
        /// </summary>
        /// <param name="address">The local address.</param>
        /// <param name="port">The UDP port to bind.</param>
        /// <param name="pskKeys">PSK identity-to-key mappings.</param>
        /// <param name="sessionIdleTimeout">The optional DTLS session idle timeout.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoapsPsk(
            IPAddress address,
            int port,
            IReadOnlyDictionary<string, string> pskKeys,
            TimeSpan? sessionIdleTimeout = null)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            ValidatePort(port);
            return ListenCoapsPsk(new IPEndPoint(address, port), pskKeys, sessionIdleTimeout);
        }

        /// <summary>
        /// Adds a DTLS PSK <c>coaps://</c> listener.
        /// </summary>
        /// <param name="endpoint">The local UDP endpoint.</param>
        /// <param name="pskKeys">PSK identity-to-key mappings.</param>
        /// <param name="sessionIdleTimeout">The optional DTLS session idle timeout.</param>
        /// <returns>The current options instance.</returns>
        public CoapServerOptions ListenCoapsPsk(
            IPEndPoint endpoint,
            IReadOnlyDictionary<string, string> pskKeys,
            TimeSpan? sessionIdleTimeout = null)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (pskKeys == null)
            {
                throw new ArgumentNullException(nameof(pskKeys));
            }

            if (pskKeys.Count == 0)
            {
                throw new ArgumentException("At least one PSK identity must be configured.", nameof(pskKeys));
            }

            ValidatePort(endpoint.Port);
            var idleTimeout = sessionIdleTimeout.GetValueOrDefault(TimeSpan.FromMinutes(5));
            if (idleTimeout <= TimeSpan.Zero)
            {
                idleTimeout = TimeSpan.FromMinutes(5);
            }

            return UseEndPoint((services, config) =>
            {
                LogListener(services, "CoAP DTLS/coaps", endpoint);
                return new CoAPEndPoint(new DtlsPskChannel(endpoint, pskKeys, idleTimeout), config);
            });
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

        private static void LogListener(
            IServiceProvider services,
            string transport,
            IPEndPoint endpoint)
        {
            var loggerFactory = services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            loggerFactory?
                .CreateLogger("CoAP.Server.Hosting")
                .LogInformation(
                    "{Transport} listener enabled on {BindAddress}:{Port}.",
                    transport,
                    endpoint.Address,
                    endpoint.Port);
        }
    }
}
