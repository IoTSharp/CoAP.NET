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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Represents the CoAP response produced by a route handler or resource action.
    /// </summary>
    public interface ICoapResult
    {
        /// <summary>
        /// Gets the CoAP response status code.
        /// </summary>
        StatusCode StatusCode { get; }

        /// <summary>
        /// Gets the response payload.
        /// </summary>
        ReadOnlyMemory<byte> Payload { get; }

        /// <summary>
        /// Gets the response Content-Format option value.
        /// </summary>
        int ContentFormat { get; }

        /// <summary>
        /// Gets ETag options attached to the response.
        /// </summary>
        IReadOnlyList<byte[]> ETags { get; }

        /// <summary>
        /// Gets the Max-Age option value, or null when it should not be set.
        /// </summary>
        long? MaxAge { get; }

        /// <summary>
        /// Gets the Location-Path option value, or null when it should not be set.
        /// </summary>
        string LocationPath { get; }

        /// <summary>
        /// Gets the Location-Query option value, or null when it should not be set.
        /// </summary>
        string LocationQuery { get; }

        /// <summary>
        /// Gets the Observe option value, or null when it should not be set.
        /// </summary>
        int? Observe { get; }
    }
}
