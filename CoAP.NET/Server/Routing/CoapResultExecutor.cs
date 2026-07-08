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
    /// Default executor that converts <see cref="ICoapResult"/> values to CoAP responses.
    /// </summary>
    public sealed class CoapResultExecutor : ICoapResultExecutor
    {
        /// <inheritdoc />
        public ValueTask ExecuteAsync(CoapResultExecutionContext context, ICoapResult result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            result ??= CoapRouteResult.Text(
                StatusCode.InternalServerError,
                "CoAP route handler returned no result.");

            var response = new Response(result.StatusCode);
            if (!result.Payload.IsEmpty)
            {
                response.Payload = result.Payload.ToArray();
                if (result.ContentFormat != MediaType.Undefined)
                {
                    response.ContentType = result.ContentFormat;
                }
            }

            if (result.MaxAge.HasValue)
            {
                response.MaxAge = result.MaxAge.Value;
            }

            if (!string.IsNullOrEmpty(result.LocationPath))
            {
                response.LocationPath = result.LocationPath;
            }

            if (!string.IsNullOrEmpty(result.LocationQuery))
            {
                response.LocationQuery = result.LocationQuery;
            }

            if (result.Observe.HasValue)
            {
                response.Observe = result.Observe.Value;
            }

            if (result.ETags != null)
            {
                foreach (var eTag in result.ETags)
                {
                    response.AddETag(eTag);
                }
            }

            context.Exchange.SendResponse(response);
            return default;
        }
    }
}
