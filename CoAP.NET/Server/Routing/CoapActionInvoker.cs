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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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

            var filters = GetFilters(context);
            if (filters.Count == 0)
            {
                return await InvokeEndpointAsync(context).ConfigureAwait(false);
            }

            CoapEndpointFilterDelegate next = InvokeEndpointAsync;
            for (var i = filters.Count - 1; i >= 0; i--)
            {
                var filter = filters[i];
                var currentNext = next;
                next = invocationContext => filter.InvokeAsync(invocationContext, currentNext);
            }

            return await next(context).ConfigureAwait(false);
        }

        private static async ValueTask<ICoapResult> InvokeEndpointAsync(CoapActionInvocationContext context)
        {
            return await context.Endpoint
                .InvokeAsync(context.RouteContext)
                .ConfigureAwait(false);
        }

        private IReadOnlyList<ICoapEndpointFilter> GetFilters(CoapActionInvocationContext context)
        {
            var serviceFilters = context.RequestServices == null
                ? Array.Empty<ICoapEndpointFilter>()
                : context.RequestServices.GetServices<ICoapEndpointFilter>()
                    .Where(filter => filter != null)
                    .ToArray();
            var metadataFilters = context.Endpoint.Metadata.OfType<ICoapEndpointFilter>().ToArray();
            if (serviceFilters.Length == 0)
            {
                return metadataFilters;
            }

            if (metadataFilters.Length == 0)
            {
                return serviceFilters;
            }

            var filters = new ICoapEndpointFilter[serviceFilters.Length + metadataFilters.Length];
            Array.Copy(serviceFilters, filters, serviceFilters.Length);
            Array.Copy(metadataFilters, 0, filters, serviceFilters.Length, metadataFilters.Length);
            return filters;
        }
    }
}
