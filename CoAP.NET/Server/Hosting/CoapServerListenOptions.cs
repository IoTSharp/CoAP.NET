/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Generic;
using System.Net;

namespace CoAP.Server.Hosting
{
    /// <summary>
    /// Configuration-bound listen settings for a hosted CoAP server.
    /// </summary>
    public class CoapServerListenOptions : CoapConfig
    {
        /// <summary>
        /// Gets or sets whether the UDP <c>coap://</c> listener is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the UDP <c>coap://</c> bind address.
        /// </summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// Gets or sets the UDP <c>coap://</c> port. When omitted, <see cref="CoapConfig.DefaultPort"/> is used.
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Gets or sets DTLS PSK <c>coaps://</c> listen settings.
        /// </summary>
        public CoapServerDtlsPskOptions Dtls { get; set; } = new CoapServerDtlsPskOptions();

        /// <summary>
        /// Gets whether at least one CoAP transport is enabled.
        /// </summary>
        public bool HasEnabledTransport => Enabled || (Dtls?.Enabled ?? false);

        /// <summary>
        /// Gets the effective UDP <c>coap://</c> port.
        /// </summary>
        /// <returns>The configured or default CoAP port.</returns>
        public int GetCoapPort()
        {
            return Port ?? DefaultPort;
        }

        /// <summary>
        /// Gets the effective DTLS PSK <c>coaps://</c> port.
        /// </summary>
        /// <returns>The configured or default CoAPS port.</returns>
        public int GetCoapsPort()
        {
            return Dtls?.Port ?? DefaultSecurePort;
        }

        /// <summary>
        /// Gets the UDP <c>coap://</c> bind address.
        /// </summary>
        /// <returns>The parsed bind address.</returns>
        public IPAddress GetCoapBindAddress()
        {
            return ParseBindAddress(BindAddress, "CoapServer:BindAddress");
        }

        /// <summary>
        /// Gets the DTLS PSK <c>coaps://</c> bind address.
        /// </summary>
        /// <returns>The parsed bind address.</returns>
        public IPAddress GetCoapsBindAddress()
        {
            var value = Dtls == null || string.IsNullOrWhiteSpace(Dtls.BindAddress)
                ? BindAddress
                : Dtls.BindAddress;
            return ParseBindAddress(value, "CoapServer:Dtls:BindAddress");
        }

        /// <summary>
        /// Validates listen settings before endpoints are created.
        /// </summary>
        public void Validate()
        {
            Validate("CoapServer");
        }

        /// <summary>
        /// Validates listen settings before endpoints are created.
        /// </summary>
        /// <param name="sectionName">The configuration section name used in exception messages.</param>
        public void Validate(string sectionName)
        {
            sectionName = string.IsNullOrWhiteSpace(sectionName) ? "CoapServer" : sectionName;
            if (!HasEnabledTransport)
            {
                return;
            }

            if (Enabled)
            {
                _ = ParseBindAddress(BindAddress, sectionName + ":BindAddress");
                ValidatePort(GetCoapPort(), sectionName + ":Port");
            }

            if (Dtls?.Enabled == true)
            {
                var bindAddressName = string.IsNullOrWhiteSpace(Dtls.BindAddress)
                    ? sectionName + ":BindAddress"
                    : sectionName + ":Dtls:BindAddress";
                _ = ParseBindAddress(
                    string.IsNullOrWhiteSpace(Dtls.BindAddress) ? BindAddress : Dtls.BindAddress,
                    bindAddressName);
                ValidatePort(GetCoapsPort(), sectionName + ":Dtls:Port");

                if (Dtls.PskKeys == null || Dtls.PskKeys.Count == 0)
                {
                    throw new InvalidOperationException(
                        "CoAP DTLS/coaps requires at least one PSK identity in " +
                        sectionName +
                        ":Dtls:PskKeys.");
                }

                foreach (var pair in Dtls.PskKeys)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    {
                        throw new InvalidOperationException(
                            sectionName +
                            ":Dtls:PskKeys cannot contain empty identity or key values.");
                    }
                }
            }
        }

        /// <summary>
        /// Parses a configuration bind address value.
        /// </summary>
        /// <param name="value">The configured value.</param>
        /// <param name="name">The configuration key name for error messages.</param>
        /// <returns>The parsed IP address.</returns>
        public static IPAddress ParseBindAddress(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "*" || value == "+")
            {
                return IPAddress.Any;
            }

            if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(value, out var address))
            {
                return address;
            }

            throw new InvalidOperationException(
                (string.IsNullOrWhiteSpace(name) ? "BindAddress" : name) +
                " must be an IP address, localhost, * or +.");
        }

        /// <summary>
        /// Validates a UDP port number.
        /// </summary>
        /// <param name="port">The port number.</param>
        /// <param name="name">The configuration key name for error messages.</param>
        public static void ValidatePort(int port, string name)
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new InvalidOperationException(
                    (string.IsNullOrWhiteSpace(name) ? "Port" : name) +
                    " must be between 0 and 65535.");
            }
        }
    }

    /// <summary>
    /// DTLS PSK <c>coaps://</c> listen settings.
    /// </summary>
    public class CoapServerDtlsPskOptions
    {
        /// <summary>
        /// Gets or sets whether the DTLS PSK <c>coaps://</c> listener is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the DTLS PSK <c>coaps://</c> bind address. Empty values reuse the UDP CoAP bind address.
        /// </summary>
        public string BindAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the DTLS PSK <c>coaps://</c> port. When omitted, <see cref="CoapConfig.DefaultSecurePort"/> is used.
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Gets or sets PSK identity-to-key mappings.
        /// </summary>
        public Dictionary<string, string> PskKeys { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the idle timeout, in seconds, for DTLS session cleanup.
        /// </summary>
        public int SessionIdleSeconds { get; set; } = 300;

        /// <summary>
        /// Gets the effective session idle timeout.
        /// </summary>
        /// <returns>The configured timeout, clamped to at least 30 seconds.</returns>
        public TimeSpan GetSessionIdleTimeout()
        {
            return TimeSpan.FromSeconds(Math.Max(30, SessionIdleSeconds));
        }
    }
}
