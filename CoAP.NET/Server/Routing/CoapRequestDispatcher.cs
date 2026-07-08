/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using CoAP.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Dispatches a CoAP exchange to the matched endpoint invocation pipeline.
    /// </summary>
    public sealed class CoapRequestDispatcher
    {
        private static readonly ILogger Log = CoapLogging.CreateLogger(typeof(CoapRequestDispatcher));
        private readonly ICoapEndpointDataSource _dataSource;
        private readonly ICoapEndpointMatcher _matcher;
        private readonly CoapActionInvoker _actionInvoker;
        private readonly ICoapResultExecutor _resultExecutor;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        /// <summary>
        /// Creates a CoAP request dispatcher.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        /// <param name="matcher">The endpoint matcher.</param>
        /// <param name="actionInvoker">The action invoker.</param>
        /// <param name="resultExecutor">The result executor.</param>
        /// <param name="serviceScopeFactory">The optional service scope factory.</param>
        public CoapRequestDispatcher(
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher,
            CoapActionInvoker actionInvoker,
            ICoapResultExecutor resultExecutor,
            IServiceScopeFactory serviceScopeFactory = null)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            _actionInvoker = actionInvoker ?? throw new ArgumentNullException(nameof(actionInvoker));
            _resultExecutor = resultExecutor ?? throw new ArgumentNullException(nameof(resultExecutor));
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Creates a dispatcher using the default action invoker and result executor.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        /// <param name="matcher">The endpoint matcher.</param>
        /// <returns>A dispatcher instance.</returns>
        public static CoapRequestDispatcher CreateDefault(
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher)
        {
            return new CoapRequestDispatcher(
                dataSource,
                matcher,
                new CoapActionInvoker(),
                new CoapResultExecutor());
        }

        /// <summary>
        /// Matches and dispatches a CoAP exchange.
        /// </summary>
        /// <param name="exchange">The CoAP exchange.</param>
        /// <param name="pathSegments">The URI path segments selected by the resource tree.</param>
        /// <returns>A task that completes when the response has been sent.</returns>
        public async ValueTask DispatchAsync(
            Exchange exchange,
            IReadOnlyList<string> pathSegments)
        {
            if (exchange == null)
            {
                throw new ArgumentNullException(nameof(exchange));
            }

            if (pathSegments == null)
            {
                throw new ArgumentNullException(nameof(pathSegments));
            }

            IServiceScope scope = null;
            try
            {
                scope = _serviceScopeFactory?.CreateScope();
                var requestServices = scope?.ServiceProvider;
                var request = exchange.Request;
                if (request == null)
                {
                    throw new CoapBadRequestException("Missing CoAP request.");
                }

                var matchContext = new CoapEndpointMatchContext(
                    request.Method,
                    pathSegments,
                    request.ContentFormat,
                    request.Accept,
                    request.Observe);

                if (!_matcher.TryMatch(matchContext, out var match))
                {
                    await ExecuteAsync(
                        exchange,
                        null,
                        CreateMatchFailureResult(matchContext),
                        requestServices)
                        .ConfigureAwait(false);
                    return;
                }

                var requestOptions = GetRequestOptions(request);
                var routeContext = new CoapRouteContext(
                    match.Endpoint,
                    request.Method,
                    pathSegments,
                    match.RouteValues,
                    GetUriQueries(request),
                    request.Payload == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(request.Payload),
                    request.ContentFormat,
                    request.Accept,
                    requestServices,
                    requestOptions,
                    request.Observe,
                    request.Source,
                    request.Token);

                await DispatchAsync(exchange, routeContext, requestServices).ConfigureAwait(false);
            }
            catch (CoapRequestException ex)
            {
                Log.LogWarning(ex, "CoAP route request failed.");
                await ExecuteAsync(
                    exchange,
                    null,
                    CoapRouteResult.Text(ex.StatusCode, ex.Message),
                    null)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "CoAP route handler failed. Path={Path}", string.Join("/", pathSegments));
                await ExecuteAsync(
                    exchange,
                    null,
                    CoapRouteResult.Text(StatusCode.InternalServerError, "CoAP route handler failed."),
                    null)
                    .ConfigureAwait(false);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        /// <summary>
        /// Dispatches an already matched route context.
        /// </summary>
        /// <param name="exchange">The CoAP exchange.</param>
        /// <param name="routeContext">The matched route context.</param>
        /// <returns>A task that completes when the response has been sent.</returns>
        public ValueTask DispatchAsync(
            Exchange exchange,
            CoapRouteContext routeContext)
        {
            if (routeContext == null)
            {
                throw new ArgumentNullException(nameof(routeContext));
            }

            return DispatchAsync(exchange, routeContext, routeContext.RequestServices);
        }

        private async ValueTask DispatchAsync(
            Exchange exchange,
            CoapRouteContext routeContext,
            IServiceProvider requestServices)
        {
            var invocationContext = new CoapActionInvocationContext(routeContext, requestServices);
            var result = await _actionInvoker
                .InvokeAsync(invocationContext)
                .ConfigureAwait(false);
            await ExecuteAsync(exchange, routeContext, result, requestServices).ConfigureAwait(false);
        }

        private ValueTask ExecuteAsync(
            Exchange exchange,
            CoapRouteContext routeContext,
            ICoapResult result,
            IServiceProvider requestServices)
        {
            return _resultExecutor.ExecuteAsync(
                new CoapResultExecutionContext(exchange, routeContext, requestServices),
                result);
        }

        private ICoapResult CreateMatchFailureResult(CoapEndpointMatchContext context)
        {
            var routeMatched = false;
            var methodMatched = false;
            var contentFormatMatched = false;
            var acceptMatched = false;
            foreach (var endpoint in _dataSource.Endpoints)
            {
                if (!endpoint.RoutePattern.TryMatch(context.PathSegments, out _))
                {
                    continue;
                }

                routeMatched = true;
                if (endpoint.Method == context.Method)
                {
                    methodMatched = true;
                    if (!CoapEndpointMatcher.EndpointAcceptsContentFormat(endpoint, context.ContentFormat))
                    {
                        continue;
                    }

                    contentFormatMatched = true;
                    if (!CoapEndpointMatcher.EndpointProducesAcceptedContent(endpoint, context.Accept))
                    {
                        continue;
                    }

                    acceptMatched = true;
                    break;
                }
            }

            if (!routeMatched)
            {
                return CoapRouteResult.Text(
                    StatusCode.NotFound,
                    "CoAP route endpoint was not found.");
            }

            if (!methodMatched)
            {
                return CoapRouteResult.Text(
                    StatusCode.MethodNotAllowed,
                    "CoAP route method is not allowed.");
            }

            if (!contentFormatMatched)
            {
                return CoapRouteResult.Text(
                    StatusCode.UnsupportedMediaType,
                    "CoAP route media type is not supported.");
            }

            if (!acceptMatched)
            {
                return CoapRouteResult.Text(
                    StatusCode.NotAcceptable,
                    "CoAP route response media type is not acceptable.");
            }

            return CoapRouteResult.Text(
                StatusCode.UnsupportedMediaType,
                "CoAP route media type is not supported.");
        }

        private static IReadOnlyList<Option> GetRequestOptions(Request request)
        {
            var options = request.GetOptions();
            if (options == null)
            {
                return Array.Empty<Option>();
            }

            if (options is ICollection<Option> collection)
            {
                if (collection.Count == 0)
                {
                    return Array.Empty<Option>();
                }

                var optionArray = new Option[collection.Count];
                var index = 0;
                foreach (var option in collection)
                {
                    optionArray[index++] = option;
                }

                return optionArray;
            }

            return options.ToArray();
        }

        private static IReadOnlyList<string> GetUriQueries(Request request)
        {
            var queryOptions = request.GetOptions(OptionType.UriQuery);
            if (queryOptions == null)
            {
                return Array.Empty<string>();
            }

            if (queryOptions is ICollection<Option> collection)
            {
                if (collection.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var queries = new string[collection.Count];
                var index = 0;
                foreach (var option in collection)
                {
                    queries[index++] = option.StringValue;
                }

                return queries;
            }

            return queryOptions.Select(option => option.StringValue).ToArray();
        }
    }
}
