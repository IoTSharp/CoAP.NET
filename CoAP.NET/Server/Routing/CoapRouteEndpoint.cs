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

using CoAP.Net;
using CoAP.Observe;
using CoAP.Server.Resources;
using CoAP.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Handles a matched CoAP route.
    /// </summary>
    /// <param name="context">The matched route context.</param>
    /// <returns>The response to send to the CoAP client.</returns>
    public delegate ValueTask<CoapRouteResult> CoapRouteHandler(CoapRouteContext context);

    /// <summary>
    /// Handles a matched CoAP route with a resource class instance.
    /// </summary>
    /// <typeparam name="TResource">The CoAP resource class type.</typeparam>
    /// <param name="resource">The resource instance for the current invocation.</param>
    /// <returns>The response to send to the CoAP client.</returns>
    public delegate ValueTask<CoapRouteResult> CoapResourceRouteHandler<in TResource>(TResource resource)
        where TResource : CoapResourceBase;

    /// <summary>
    /// Describes a route template and handler for the CoAP resource tree adapter.
    /// </summary>
    public sealed class CoapRoute
    {
        /// <summary>
        /// Creates a CoAP route.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="template">The URI path template, such as db/{db}/m/{measurement}.</param>
        /// <param name="handler">The route handler.</param>
        public CoapRoute(Method method, string template, CoapRouteHandler handler)
        {
            Endpoint = new CoapEndpoint(
                method,
                CoapRoutePattern.Parse(template),
                handler,
                new object[] { this });
        }

        /// <summary>
        /// The CoAP request method.
        /// </summary>
        public Method Method => Endpoint.Method;

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template => Endpoint.RoutePattern.Template;

        /// <summary>
        /// The endpoint descriptor created for this route.
        /// </summary>
        public CoapEndpoint Endpoint { get; }

        /// <summary>
        /// The first URI path segment used as the root resource.
        /// </summary>
        public string RootSegment => Endpoint.RootSegment;

        internal bool IsPrefix(IReadOnlyList<string> pathSegments)
        {
            return Endpoint.RoutePattern.IsPrefix(pathSegments);
        }

        internal bool TryMatch(Method method, IReadOnlyList<string> pathSegments, out IReadOnlyDictionary<string, string> routeValues)
        {
            routeValues = null;
            return method == Method && Endpoint.RoutePattern.TryMatch(pathSegments, out routeValues);
        }

        internal ValueTask<CoapRouteResult> InvokeAsync(CoapRouteContext context)
        {
            return Endpoint.InvokeAsync(context);
        }

        /// <summary>
        /// Creates a POST route.
        /// </summary>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The route handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Post(string template, CoapRouteHandler handler)
        {
            return new CoapRoute(Method.POST, template, handler);
        }

        /// <summary>
        /// Creates a POST route backed by a CoAP resource class.
        /// </summary>
        /// <typeparam name="TResource">The CoAP resource class type.</typeparam>
        /// <param name="template">The URI path template.</param>
        /// <param name="resourceFactory">Factory that creates the resource instance for this invocation.</param>
        /// <param name="handler">The resource action handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Post<TResource>(
            string template,
            Func<TResource> resourceFactory,
            CoapResourceRouteHandler<TResource> handler)
            where TResource : CoapResourceBase
        {
            return CreateResourceRoute(Method.POST, template, resourceFactory, handler);
        }

        /// <summary>
        /// Creates a GET route.
        /// </summary>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The route handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Get(string template, CoapRouteHandler handler)
        {
            return new CoapRoute(Method.GET, template, handler);
        }

        /// <summary>
        /// Creates a GET route backed by a CoAP resource class.
        /// </summary>
        /// <typeparam name="TResource">The CoAP resource class type.</typeparam>
        /// <param name="template">The URI path template.</param>
        /// <param name="resourceFactory">Factory that creates the resource instance for this invocation.</param>
        /// <param name="handler">The resource action handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Get<TResource>(
            string template,
            Func<TResource> resourceFactory,
            CoapResourceRouteHandler<TResource> handler)
            where TResource : CoapResourceBase
        {
            return CreateResourceRoute(Method.GET, template, resourceFactory, handler);
        }

        /// <summary>
        /// Creates a PUT route.
        /// </summary>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The route handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Put(string template, CoapRouteHandler handler)
        {
            return new CoapRoute(Method.PUT, template, handler);
        }

        /// <summary>
        /// Creates a PUT route backed by a CoAP resource class.
        /// </summary>
        /// <typeparam name="TResource">The CoAP resource class type.</typeparam>
        /// <param name="template">The URI path template.</param>
        /// <param name="resourceFactory">Factory that creates the resource instance for this invocation.</param>
        /// <param name="handler">The resource action handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Put<TResource>(
            string template,
            Func<TResource> resourceFactory,
            CoapResourceRouteHandler<TResource> handler)
            where TResource : CoapResourceBase
        {
            return CreateResourceRoute(Method.PUT, template, resourceFactory, handler);
        }

        /// <summary>
        /// Creates a DELETE route.
        /// </summary>
        /// <param name="template">The URI path template.</param>
        /// <param name="handler">The route handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Delete(string template, CoapRouteHandler handler)
        {
            return new CoapRoute(Method.DELETE, template, handler);
        }

        /// <summary>
        /// Creates a DELETE route backed by a CoAP resource class.
        /// </summary>
        /// <typeparam name="TResource">The CoAP resource class type.</typeparam>
        /// <param name="template">The URI path template.</param>
        /// <param name="resourceFactory">Factory that creates the resource instance for this invocation.</param>
        /// <param name="handler">The resource action handler.</param>
        /// <returns>A route descriptor.</returns>
        public static CoapRoute Delete<TResource>(
            string template,
            Func<TResource> resourceFactory,
            CoapResourceRouteHandler<TResource> handler)
            where TResource : CoapResourceBase
        {
            return CreateResourceRoute(Method.DELETE, template, resourceFactory, handler);
        }

        private static CoapRoute CreateResourceRoute<TResource>(
            Method method,
            string template,
            Func<TResource> resourceFactory,
            CoapResourceRouteHandler<TResource> handler)
            where TResource : CoapResourceBase
        {
            if (resourceFactory == null)
            {
                throw new ArgumentNullException(nameof(resourceFactory));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return new CoapRoute(method, template, async context =>
            {
                var resource = resourceFactory();
                if (resource == null)
                {
                    throw new InvalidOperationException("The CoAP resource factory returned null.");
                }

                return await resource.InvokeWithContextAsync(context, () => handler(resource)).ConfigureAwait(false);
            });
        }
    }

    /// <summary>
    /// Context passed to a CoAP route handler after method and URI path matching.
    /// </summary>
    public sealed class CoapRouteContext
    {
        /// <summary>
        /// Creates a matched CoAP route context.
        /// </summary>
        /// <param name="route">The route descriptor.</param>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="pathSegments">The URI path segments.</param>
        /// <param name="routeValues">Values extracted from template parameters.</param>
        /// <param name="queries">The URI query options.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="contentFormat">The Content-Format option value.</param>
        /// <param name="accept">The Accept option value.</param>
        public CoapRouteContext(
            CoapRoute route,
            Method method,
            IReadOnlyList<string> pathSegments,
            IReadOnlyDictionary<string, string> routeValues,
            IReadOnlyList<string> queries,
            ReadOnlyMemory<byte> payload,
            int contentFormat,
            int accept,
            IServiceProvider requestServices = null,
            IReadOnlyList<Option> options = null,
            int? observe = null,
            System.Net.EndPoint remoteEndPoint = null,
            byte[] token = null)
            : this(
                  route == null ? null : route.Endpoint,
                  route,
                  method,
                  pathSegments,
                  routeValues,
                  queries,
                  payload,
                  contentFormat,
                  accept,
                  requestServices,
                  options,
                  observe,
                  remoteEndPoint,
                  token)
        {
        }

        /// <summary>
        /// Creates a matched CoAP endpoint context.
        /// </summary>
        /// <param name="endpoint">The selected endpoint.</param>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="pathSegments">The URI path segments.</param>
        /// <param name="routeValues">Values extracted from template parameters.</param>
        /// <param name="queries">The URI query options.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="contentFormat">The Content-Format option value.</param>
        /// <param name="accept">The Accept option value.</param>
        public CoapRouteContext(
            CoapEndpoint endpoint,
            Method method,
            IReadOnlyList<string> pathSegments,
            IReadOnlyDictionary<string, string> routeValues,
            IReadOnlyList<string> queries,
            ReadOnlyMemory<byte> payload,
            int contentFormat,
            int accept,
            IServiceProvider requestServices = null,
            IReadOnlyList<Option> options = null,
            int? observe = null,
            System.Net.EndPoint remoteEndPoint = null,
            byte[] token = null)
            : this(
                  endpoint,
                  endpoint == null ? null : endpoint.Metadata.GetMetadata<CoapRoute>(),
                  method,
                  pathSegments,
                  routeValues,
                  queries,
                  payload,
                  contentFormat,
                  accept,
                  requestServices,
                  options,
                  observe,
                  remoteEndPoint,
                  token)
        {
        }

        private CoapRouteContext(
            CoapEndpoint endpoint,
            CoapRoute route,
            Method method,
            IReadOnlyList<string> pathSegments,
            IReadOnlyDictionary<string, string> routeValues,
            IReadOnlyList<string> queries,
            ReadOnlyMemory<byte> payload,
            int contentFormat,
            int accept,
            IServiceProvider requestServices,
            IReadOnlyList<Option> options,
            int? observe,
            System.Net.EndPoint remoteEndPoint,
            byte[] token)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Route = route;
            Method = method;
            PathSegments = pathSegments;
            RouteValues = routeValues;
            Queries = queries;
            Payload = payload;
            ContentFormat = contentFormat;
            Accept = accept;
            RequestServices = requestServices;
            Options = options ?? Array.Empty<Option>();
            Observe = observe;
            RemoteEndPoint = remoteEndPoint;
            Token = token == null ? null : token.ToArray();
            ETags = CreateOpaqueOptionValues(Options, OptionType.ETag);
            Block1 = GetBlockOption(Options, OptionType.Block1);
            Block2 = GetBlockOption(Options, OptionType.Block2);
        }

        /// <summary>
        /// The matched endpoint descriptor.
        /// </summary>
        public CoapEndpoint Endpoint { get; }

        /// <summary>
        /// The matched route descriptor.
        /// </summary>
        public CoapRoute Route { get; }

        /// <summary>
        /// The CoAP request method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// The URI path segments.
        /// </summary>
        public IReadOnlyList<string> PathSegments { get; }

        /// <summary>
        /// Values extracted from route template parameters.
        /// </summary>
        public IReadOnlyDictionary<string, string> RouteValues { get; }

        /// <summary>
        /// The URI query options.
        /// </summary>
        public IReadOnlyList<string> Queries { get; }

        /// <summary>
        /// The request payload.
        /// </summary>
        public ReadOnlyMemory<byte> Payload { get; }

        /// <summary>
        /// The Content-Format option value.
        /// </summary>
        public int ContentFormat { get; }

        /// <summary>
        /// The Accept option value.
        /// </summary>
        public int Accept { get; }

        /// <summary>
        /// The Observe option value, or null when the request does not carry Observe.
        /// </summary>
        public int? Observe { get; }

        /// <summary>
        /// Snapshot of request options visible to the routing layer.
        /// </summary>
        public IReadOnlyList<Option> Options { get; }

        /// <summary>
        /// ETag option values carried by the request.
        /// </summary>
        public IReadOnlyList<byte[]> ETags { get; }

        /// <summary>
        /// The request Block1 option, or null when absent.
        /// </summary>
        public BlockOption Block1 { get; }

        /// <summary>
        /// The request Block2 option, or null when absent.
        /// </summary>
        public BlockOption Block2 { get; }

        /// <summary>
        /// The remote endpoint that sent the request, if known.
        /// </summary>
        public System.Net.EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// The request token bytes, or null when no token was supplied.
        /// </summary>
        public byte[] Token { get; }

        /// <summary>
        /// The scoped service provider for this request, if the route was invoked by a host-integrated server.
        /// </summary>
        public IServiceProvider RequestServices { get; }

        /// <summary>
        /// Enumerates request options with the specified type.
        /// </summary>
        /// <param name="optionType">The option type to find.</param>
        /// <returns>Matching request options in request order.</returns>
        public IEnumerable<Option> GetOptions(OptionType optionType)
        {
            for (var i = 0; i < Options.Count; i++)
            {
                var option = Options[i];
                if (option != null && option.Type == optionType)
                {
                    yield return option;
                }
            }
        }

        /// <summary>
        /// Gets the first request option with the specified type.
        /// </summary>
        /// <param name="optionType">The option type to find.</param>
        /// <returns>The first matching option, or null.</returns>
        public Option GetFirstOption(OptionType optionType)
        {
            for (var i = 0; i < Options.Count; i++)
            {
                var option = Options[i];
                if (option != null && option.Type == optionType)
                {
                    return option;
                }
            }

            return null;
        }

        private static IReadOnlyList<byte[]> CreateOpaqueOptionValues(
            IReadOnlyList<Option> options,
            OptionType optionType)
        {
            if (options == null || options.Count == 0)
            {
                return Array.Empty<byte[]>();
            }

            var values = new List<byte[]>();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option != null && option.Type == optionType)
                {
                    values.Add(option.RawValue == null ? null : option.RawValue.ToArray());
                }
            }

            return values.Count == 0 ? Array.Empty<byte[]>() : values.ToArray();
        }

        private static BlockOption GetBlockOption(
            IReadOnlyList<Option> options,
            OptionType optionType)
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] is BlockOption blockOption && blockOption.Type == optionType)
                {
                    return blockOption;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// CoAP route handler response.
    /// </summary>
    public sealed class CoapRouteResult : ICoapResult
    {
        private CoapRouteResult(
            StatusCode statusCode,
            ReadOnlyMemory<byte> payload,
            int contentFormat,
            IReadOnlyList<byte[]> eTags,
            long? maxAge,
            string locationPath,
            string locationQuery,
            int? observe)
        {
            StatusCode = statusCode;
            Payload = payload;
            ContentFormat = contentFormat;
            ETags = eTags ?? Array.Empty<byte[]>();
            MaxAge = maxAge;
            LocationPath = locationPath;
            LocationQuery = locationQuery;
            Observe = observe;
        }

        /// <summary>
        /// The CoAP response status code.
        /// </summary>
        public StatusCode StatusCode { get; }

        /// <summary>
        /// The response payload.
        /// </summary>
        public ReadOnlyMemory<byte> Payload { get; }

        /// <summary>
        /// The response Content-Format value.
        /// </summary>
        public int ContentFormat { get; }

        /// <summary>
        /// ETag options to attach to the response.
        /// </summary>
        public IReadOnlyList<byte[]> ETags { get; }

        /// <summary>
        /// Max-Age option to attach to the response, or null to leave it unset.
        /// </summary>
        public long? MaxAge { get; }

        /// <summary>
        /// Location-Path option value to attach to the response.
        /// </summary>
        public string LocationPath { get; }

        /// <summary>
        /// Location-Query option value to attach to the response.
        /// </summary>
        public string LocationQuery { get; }

        /// <inheritdoc />
        public int? Observe { get; }

        /// <summary>
        /// Creates a response without a payload.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Status(StatusCode statusCode)
        {
            return new CoapRouteResult(statusCode, ReadOnlyMemory<byte>.Empty, MediaType.Undefined, null, null, null, null, null);
        }

        /// <summary>
        /// Creates a 2.04 Changed response.
        /// </summary>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Changed()
        {
            return Status(StatusCode.Changed);
        }

        /// <summary>
        /// Creates a 2.05 Content response with a binary payload.
        /// </summary>
        /// <param name="payload">The response payload.</param>
        /// <param name="contentFormat">The response Content-Format option value.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Content(ReadOnlyMemory<byte> payload, int contentFormat)
        {
            return Bytes(StatusCode.Content, payload, contentFormat);
        }

        /// <summary>
        /// Creates a binary response.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <param name="payload">The response payload.</param>
        /// <param name="contentFormat">The response Content-Format option value.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Bytes(StatusCode statusCode, ReadOnlyMemory<byte> payload, int contentFormat)
        {
            return new CoapRouteResult(statusCode, payload, contentFormat, null, null, null, null, null);
        }

        /// <summary>
        /// Creates a text response.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <param name="message">The response body.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Text(StatusCode statusCode, string message)
        {
            var payload = string.IsNullOrEmpty(message)
                ? ReadOnlyMemory<byte>.Empty
                : Encoding.UTF8.GetBytes(message);
            return Bytes(statusCode, payload, MediaType.TextPlain);
        }

        /// <summary>
        /// Creates a 2.05 Content response with a UTF-8 JSON payload.
        /// </summary>
        /// <param name="json">The JSON response body.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Json(string json)
        {
            return Json(StatusCode.Content, json);
        }

        /// <summary>
        /// Creates a JSON response with a UTF-8 payload.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <param name="json">The JSON response body.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Json(StatusCode statusCode, string json)
        {
            var payload = string.IsNullOrEmpty(json)
                ? ReadOnlyMemory<byte>.Empty
                : Encoding.UTF8.GetBytes(json);
            return Json(statusCode, payload);
        }

        /// <summary>
        /// Creates a 2.05 Content response with a JSON payload.
        /// </summary>
        /// <param name="payload">The UTF-8 JSON response payload.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Json(ReadOnlyMemory<byte> payload)
        {
            return Json(StatusCode.Content, payload);
        }

        /// <summary>
        /// Creates a JSON response.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <param name="payload">The UTF-8 JSON response payload.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Json(StatusCode statusCode, ReadOnlyMemory<byte> payload)
        {
            return Bytes(statusCode, payload, MediaType.ApplicationJson);
        }

        /// <summary>
        /// Copies a general CoAP result into the default route result implementation.
        /// </summary>
        /// <param name="result">The result to copy.</param>
        /// <returns>A route result with equivalent response properties.</returns>
        public static CoapRouteResult FromResult(ICoapResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return new CoapRouteResult(
                result.StatusCode,
                result.Payload,
                result.ContentFormat,
                result.ETags,
                result.MaxAge,
                result.LocationPath,
                result.LocationQuery,
                result.Observe);
        }

        /// <summary>
        /// Returns a copy of this result with an ETag option appended.
        /// </summary>
        /// <param name="eTag">The ETag opaque value.</param>
        /// <returns>A route result with the supplied ETag.</returns>
        public CoapRouteResult WithETag(byte[] eTag)
        {
            if (eTag == null)
            {
                throw new ArgumentNullException(nameof(eTag));
            }

            var eTags = ETags
                .Concat(new[] { eTag.ToArray() })
                .ToArray();
            return Copy(eTags: eTags);
        }

        /// <summary>
        /// Returns a copy of this result with a Max-Age option.
        /// </summary>
        /// <param name="maxAge">The Max-Age option value in seconds.</param>
        /// <returns>A route result with the supplied Max-Age.</returns>
        public CoapRouteResult WithMaxAge(long maxAge)
        {
            if (maxAge < 0 || maxAge > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAge), "Max-Age option must be between 0 and UInt32.MaxValue.");
            }

            return Copy(maxAge: maxAge);
        }

        /// <summary>
        /// Returns a copy of this result with a Location-Path option.
        /// </summary>
        /// <param name="locationPath">The response Location-Path value.</param>
        /// <returns>A route result with the supplied Location-Path.</returns>
        public CoapRouteResult WithLocationPath(string locationPath)
        {
            if (locationPath == null)
            {
                throw new ArgumentNullException(nameof(locationPath));
            }

            return Copy(locationPath: locationPath);
        }

        /// <summary>
        /// Returns a copy of this result with a Location-Query option.
        /// </summary>
        /// <param name="locationQuery">The response Location-Query value.</param>
        /// <returns>A route result with the supplied Location-Query.</returns>
        public CoapRouteResult WithLocationQuery(string locationQuery)
        {
            if (locationQuery == null)
            {
                throw new ArgumentNullException(nameof(locationQuery));
            }

            return Copy(locationQuery: locationQuery);
        }

        /// <summary>
        /// Returns a copy of this result with an Observe option.
        /// </summary>
        /// <param name="observe">The Observe option value.</param>
        /// <returns>A route result with the supplied Observe option.</returns>
        public CoapRouteResult WithObserve(int observe)
        {
            if (observe < 0 || observe > (1 << 24) - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(observe), "Observe option must be between 0 and 16777215.");
            }

            return Copy(observe: observe);
        }

        private CoapRouteResult Copy(
            IReadOnlyList<byte[]> eTags = null,
            long? maxAge = null,
            string locationPath = null,
            string locationQuery = null,
            int? observe = null)
        {
            return new CoapRouteResult(
                StatusCode,
                Payload,
                ContentFormat,
                eTags ?? ETags,
                maxAge ?? MaxAge,
                locationPath ?? LocationPath,
                locationQuery ?? LocationQuery,
                observe ?? Observe);
        }
    }

    /// <summary>
    /// Adapts route templates to the CoAP.NET resource tree.
    /// </summary>
    public sealed class CoapRouteEndpoint : IResource
    {
        private static readonly ILogger Log = CoapLogging.CreateLogger(typeof(CoapRouteEndpoint));
        private readonly ICoapEndpointDataSource _dataSource;
        private readonly CoapRequestDispatcher _dispatcher;
        private readonly IReadOnlyList<CoapEndpoint> _candidateEndpoints;
        private readonly IReadOnlyList<string> _pathSegments;
        private readonly ResourceAttributes _attributes;
        private readonly bool _observable;
        private IReadOnlyList<IResource> _discoveryChildren;
        private IResource _parent;

        private CoapRouteEndpoint(
            string name,
            ICoapEndpointDataSource dataSource,
            CoapRequestDispatcher dispatcher,
            IReadOnlyList<CoapEndpoint> candidateEndpoints,
            IReadOnlyList<string> pathSegments)
        {
            Name = name;
            _dataSource = dataSource;
            _dispatcher = dispatcher;
            _candidateEndpoints = candidateEndpoints ?? Array.Empty<CoapEndpoint>();
            _pathSegments = pathSegments ?? Array.Empty<string>();

            var discoverableEndpoints = GetDiscoverableCompleteEndpoints(_candidateEndpoints, _pathSegments);
            _observable = HasObservableEndpoint(discoverableEndpoints);
            _attributes = CreateAttributes(discoverableEndpoints, _observable);
            Visible = discoverableEndpoints.Count > 0;
        }

        /// <summary>
        /// Creates root resources for the supplied route descriptors.
        /// </summary>
        /// <param name="routes">The route descriptors.</param>
        /// <returns>Root resources grouped by first URI path segment.</returns>
        public static IReadOnlyList<IResource> Create(IEnumerable<CoapRoute> routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            return Create(CoapEndpointDataSource.FromRoutes(routes));
        }

        /// <summary>
        /// Creates root resources for endpoints supplied by a data source.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        /// <returns>Root resources grouped by first URI path segment.</returns>
        public static IReadOnlyList<IResource> Create(ICoapEndpointDataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            return Create(dataSource, new CoapEndpointMatcher(dataSource));
        }

        /// <summary>
        /// Creates root resources for endpoints supplied by a data source.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        /// <param name="matcher">The endpoint matcher.</param>
        /// <returns>Root resources grouped by first URI path segment.</returns>
        public static IReadOnlyList<IResource> Create(
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher)
        {
            return Create(dataSource, matcher, null);
        }

        /// <summary>
        /// Creates root resources for endpoints supplied by a data source.
        /// </summary>
        /// <param name="dataSource">The endpoint data source.</param>
        /// <param name="matcher">The endpoint matcher.</param>
        /// <param name="dispatcher">The request dispatcher.</param>
        /// <returns>Root resources grouped by first URI path segment.</returns>
        public static IReadOnlyList<IResource> Create(
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher,
            CoapRequestDispatcher dispatcher)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            if (matcher == null)
            {
                throw new ArgumentNullException(nameof(matcher));
            }

            dispatcher ??= CoapRequestDispatcher.CreateDefault(dataSource, matcher);
            return dataSource.Endpoints
                .Select(endpoint => endpoint ?? throw new ArgumentException("Endpoint data source cannot contain null entries.", nameof(dataSource)))
                .GroupBy(endpoint => endpoint.RootSegment, StringComparer.Ordinal)
                .Select(group => (IResource)new CoapRouteEndpoint(
                    group.Key,
                    dataSource,
                    dispatcher,
                    group.ToArray(),
                    new[] { group.Key }))
                .ToArray();
        }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Path { get; set; } = string.Empty;

        /// <inheritdoc />
        public string Uri => Path + Name;

        /// <inheritdoc />
        public bool Visible { get; }

        /// <inheritdoc />
        public bool Cachable => false;

        /// <inheritdoc />
        public bool Observable => _observable;

        /// <inheritdoc />
        public IExecutor Executor => Parent?.Executor;

        /// <inheritdoc />
        public IEnumerable<IEndPoint> EndPoints => Parent?.EndPoints ?? Array.Empty<IEndPoint>();

        /// <inheritdoc />
        public IResource Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                Path = value == null ? string.Empty : value.Path + value.Name + "/";
            }
        }

        /// <inheritdoc />
        public ResourceAttributes Attributes => _attributes;

        /// <inheritdoc />
        public IEnumerable<IResource> Children => GetDiscoveryChildren();

        /// <inheritdoc />
        public void Add(IResource child)
        {
            throw new NotSupportedException("CoAP route endpoints are dynamic and do not accept child resources.");
        }

        /// <inheritdoc />
        public bool Remove(IResource child)
        {
            return false;
        }

        /// <inheritdoc />
        public IResource GetChild(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var nextSegments = AppendPathSegment(_pathSegments, name);
            var matchingEndpoints = _candidateEndpoints
                .Where(endpoint => endpoint.RoutePattern.IsPrefix(nextSegments))
                .ToArray();
            return matchingEndpoints.Length == 0
                ? null
                : new CoapRouteEndpoint(
                    name,
                    _dataSource,
                    _dispatcher,
                    matchingEndpoints,
                    nextSegments) { Parent = this };
        }

        /// <inheritdoc />
        public void AddObserveRelation(ObserveRelation relation)
        {
        }

        /// <inheritdoc />
        public void RemoveObserveRelation(ObserveRelation relation)
        {
        }

        /// <inheritdoc />
        public void HandleRequest(Exchange exchange)
        {
            if (exchange == null)
            {
                throw new ArgumentNullException(nameof(exchange));
            }

            try
            {
                _dispatcher.DispatchAsync(exchange, _pathSegments).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "CoAP route handler failed. Path={Path}", string.Join("/", _pathSegments));
                SendText(exchange, StatusCode.InternalServerError, "CoAP route handler failed.");
            }
        }

        private static string[] AppendPathSegment(IReadOnlyList<string> pathSegments, string segment)
        {
            var nextSegments = new string[pathSegments.Count + 1];
            for (var i = 0; i < pathSegments.Count; i++)
            {
                nextSegments[i] = pathSegments[i];
            }

            nextSegments[^1] = segment;
            return nextSegments;
        }

        private IReadOnlyList<IResource> GetDiscoveryChildren()
        {
            var children = _discoveryChildren;
            if (children != null)
            {
                return children;
            }

            var nextIndex = _pathSegments.Count;
            children = _candidateEndpoints
                .Where(endpoint => endpoint.RoutePattern.Segments.Count > nextIndex)
                .GroupBy(endpoint => endpoint.RoutePattern.Segments[nextIndex].RawText, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (IResource)new CoapRouteEndpoint(
                    group.Key,
                    _dataSource,
                    _dispatcher,
                    group.ToArray(),
                    AppendPathSegment(_pathSegments, group.Key)) { Parent = this })
                .ToArray();

            _discoveryChildren = children;
            return children;
        }

        private static IReadOnlyList<CoapEndpoint> GetDiscoverableCompleteEndpoints(
            IReadOnlyList<CoapEndpoint> candidateEndpoints,
            IReadOnlyList<string> pathSegments)
        {
            if (candidateEndpoints == null || candidateEndpoints.Count == 0)
            {
                return Array.Empty<CoapEndpoint>();
            }

            var endpoints = new List<CoapEndpoint>();
            for (var i = 0; i < candidateEndpoints.Count; i++)
            {
                var endpoint = candidateEndpoints[i];
                if (endpoint == null ||
                    IsHiddenEndpoint(endpoint) ||
                    endpoint.RoutePattern.Segments.Count != pathSegments.Count)
                {
                    continue;
                }

                if (endpoint.RoutePattern.TryMatch(pathSegments, out _))
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints.Count == 0 ? Array.Empty<CoapEndpoint>() : endpoints.ToArray();
        }

        private static ResourceAttributes CreateAttributes(
            IReadOnlyList<CoapEndpoint> endpoints,
            bool observable)
        {
            var attributes = new ResourceAttributes();
            if (endpoints == null || endpoints.Count == 0)
            {
                return attributes;
            }

            var title = GetDiscoveryTitle(endpoints);
            if (!string.IsNullOrWhiteSpace(title))
            {
                attributes.Title = title;
            }

            var resourceTypes = new HashSet<string>(StringComparer.Ordinal);
            var interfaceDescriptions = new HashSet<string>(StringComparer.Ordinal);
            var contentFormats = new HashSet<int>();
            for (var i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                AddResourceTypes(attributes, resourceTypes, endpoint);
                AddInterfaceDescriptions(attributes, interfaceDescriptions, endpoint);
                AddContentFormats(attributes, contentFormats, endpoint);
            }

            if (observable)
            {
                attributes.Observable = true;
            }

            return attributes;
        }

        private static string GetDiscoveryTitle(IReadOnlyList<CoapEndpoint> endpoints)
        {
            for (var i = 0; i < endpoints.Count; i++)
            {
                var title = endpoints[i].Metadata.GetMetadata<CoapResourceTitleAttribute>()?.Title;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }

            return null;
        }

        private static void AddResourceTypes(
            ResourceAttributes attributes,
            HashSet<string> seen,
            CoapEndpoint endpoint)
        {
            foreach (var metadata in endpoint.Metadata.OfType<CoapResourceTypeAttribute>())
            {
                for (var i = 0; i < metadata.ResourceTypes.Length; i++)
                {
                    var value = metadata.ResourceTypes[i];
                    if (seen.Add(value))
                    {
                        attributes.AddResourceType(value);
                    }
                }
            }
        }

        private static void AddInterfaceDescriptions(
            ResourceAttributes attributes,
            HashSet<string> seen,
            CoapEndpoint endpoint)
        {
            foreach (var metadata in endpoint.Metadata.OfType<CoapInterfaceDescriptionAttribute>())
            {
                for (var i = 0; i < metadata.InterfaceDescriptions.Length; i++)
                {
                    var value = metadata.InterfaceDescriptions[i];
                    if (seen.Add(value))
                    {
                        attributes.AddInterfaceDescription(value);
                    }
                }
            }
        }

        private static void AddContentFormats(
            ResourceAttributes attributes,
            HashSet<int> seen,
            CoapEndpoint endpoint)
        {
            var produces = endpoint.Metadata.GetMetadata<CoapProducesAttribute>();
            if (produces == null)
            {
                return;
            }

            for (var i = 0; i < produces.ContentFormats.Length; i++)
            {
                var contentFormat = produces.ContentFormats[i];
                if (contentFormat == MediaType.Undefined ||
                    contentFormat == MediaType.Any ||
                    !seen.Add(contentFormat))
                {
                    continue;
                }

                attributes.AddContentType(contentFormat);
            }
        }

        private static bool HasObservableEndpoint(IReadOnlyList<CoapEndpoint> endpoints)
        {
            if (endpoints == null || endpoints.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < endpoints.Count; i++)
            {
                if (IsObservableEndpoint(endpoints[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsObservableEndpoint(CoapEndpoint endpoint)
        {
            return endpoint.Metadata.GetMetadata<CoapMethodAttribute>()?.IsObserve == true;
        }

        private static bool IsHiddenEndpoint(CoapEndpoint endpoint)
        {
            return endpoint.Metadata.GetMetadata<CoapResourceHiddenAttribute>() != null;
        }

        private static void SendText(Exchange exchange, StatusCode statusCode, string message)
        {
            var response = new Response(statusCode);
            response.SetPayload(message, MediaType.TextPlain);
            exchange.SendResponse(response);
        }
    }
}
