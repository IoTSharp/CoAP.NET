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

using System.Collections.Generic;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Provides CoAP endpoints to the resource tree adapter and matcher.
    /// </summary>
    public interface ICoapEndpointDataSource
    {
        /// <summary>
        /// Gets the currently available CoAP endpoints.
        /// </summary>
        IReadOnlyList<CoapEndpoint> Endpoints { get; }
    }
}
