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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Selects a CoAP endpoint for a request.
    /// </summary>
    public interface ICoapEndpointMatcher
    {
        /// <summary>
        /// Attempts to match a request to a CoAP endpoint.
        /// </summary>
        /// <param name="context">The matching input.</param>
        /// <param name="match">The selected endpoint and route values.</param>
        /// <returns><c>true</c> when an endpoint is selected.</returns>
        bool TryMatch(CoapEndpointMatchContext context, out CoapEndpointMatch match);
    }
}
