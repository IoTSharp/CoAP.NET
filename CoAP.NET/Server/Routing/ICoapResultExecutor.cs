/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Writes an <see cref="ICoapResult"/> to the CoAP exchange response.
    /// </summary>
    public interface ICoapResultExecutor
    {
        /// <summary>
        /// Executes the result for the current exchange.
        /// </summary>
        /// <param name="context">The result execution context.</param>
        /// <param name="result">The result to execute.</param>
        /// <returns>A task that completes when the response has been sent.</returns>
        ValueTask ExecuteAsync(CoapResultExecutionContext context, ICoapResult result);
    }
}
