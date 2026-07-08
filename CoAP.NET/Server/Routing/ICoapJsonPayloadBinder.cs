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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Binds a UTF-8 JSON CoAP payload to a resource action parameter.
    /// </summary>
    public interface ICoapJsonPayloadBinder
    {
        /// <summary>
        /// Deserializes the current request payload to the requested model type.
        /// </summary>
        /// <param name="modelType">The action parameter type to create.</param>
        /// <param name="context">The current CoAP route context.</param>
        /// <returns>The bound model value.</returns>
        object Bind(Type modelType, CoapRouteContext context);
    }
}
