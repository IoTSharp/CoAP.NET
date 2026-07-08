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
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Invokes the handler or resource action for a matched CoAP endpoint.
    /// </summary>
    public class CoapActionInvoker
    {
        /// <summary>
        /// Invokes the selected endpoint.
        /// </summary>
        /// <param name="context">The action invocation context.</param>
        /// <returns>The action result.</returns>
        public virtual async ValueTask<ICoapResult> InvokeAsync(CoapActionInvocationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return await context.Endpoint
                .InvokeAsync(context.RouteContext)
                .ConfigureAwait(false);
        }
    }
}
