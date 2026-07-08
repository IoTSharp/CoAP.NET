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
        private readonly RouteSegment[] _segments;
        private readonly string[] _parameterNames;
        private readonly CoapRouteHandler _handler;

        /// <summary>
        /// Creates a CoAP route.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="template">The URI path template, such as db/{db}/m/{measurement}.</param>
        /// <param name="handler">The route handler.</param>
        public CoapRoute(Method method, string template, CoapRouteHandler handler)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Route template is required.", nameof(template));
            }

            Method = method;
            Template = template.Trim('/');
            _segments = ParseTemplate(Template, out _parameterNames);
            if (_segments.Length == 0)
            {
                throw new ArgumentException("Route template must contain at least one segment.", nameof(template));
            }

            if (_segments[0].IsParameter)
            {
                throw new ArgumentException("Route template root segment must be a literal resource name.", nameof(template));
            }

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// The CoAP request method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// The route template.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// The first URI path segment used as the root resource.
        /// </summary>
        public string RootSegment => _segments[0].Value;

        internal bool IsPrefix(IReadOnlyList<string> pathSegments)
        {
            if (pathSegments == null || pathSegments.Count > _segments.Length)
            {
                return false;
            }

            for (var i = 0; i < pathSegments.Count; i++)
            {
                if (!_segments[i].Matches(pathSegments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryMatch(Method method, IReadOnlyList<string> pathSegments, out IReadOnlyDictionary<string, string> routeValues)
        {
            routeValues = null;
            if (method != Method || pathSegments == null || pathSegments.Count != _segments.Length)
            {
                return false;
            }

            for (var i = 0; i < pathSegments.Count; i++)
            {
                var segment = _segments[i];
                if (!segment.Matches(pathSegments[i]))
                {
                    return false;
                }
            }

            if (_parameterNames.Length == 0)
            {
                routeValues = CoapRouteValueCollection.Empty;
                return true;
            }

            var values = new string[_parameterNames.Length];
            for (var i = 0; i < pathSegments.Count; i++)
            {
                var segment = _segments[i];
                if (segment.IsParameter)
                {
                    values[segment.ParameterIndex] = pathSegments[i];
                }
            }

            routeValues = new CoapRouteValueCollection(_parameterNames, values);
            return true;
        }

        internal ValueTask<CoapRouteResult> InvokeAsync(CoapRouteContext context)
        {
            return _handler(context);
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

        private static RouteSegment[] ParseTemplate(string template, out string[] parameterNames)
        {
            var rawSegments = template.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var segments = new RouteSegment[rawSegments.Length];
            var parameters = new List<string>();
            for (var i = 0; i < rawSegments.Length; i++)
            {
                segments[i] = RouteSegment.Parse(rawSegments[i], parameters);
            }

            parameterNames = parameters.Count == 0 ? Array.Empty<string>() : parameters.ToArray();
            return segments;
        }

        private static int GetOrAddParameterIndex(List<string> parameterNames, string parameterName)
        {
            for (var i = 0; i < parameterNames.Count; i++)
            {
                if (string.Equals(parameterNames[i], parameterName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            parameterNames.Add(parameterName);
            return parameterNames.Count - 1;
        }

        private readonly struct RouteSegment
        {
            private RouteSegment(string value, bool isParameter, int parameterIndex)
            {
                Value = value;
                IsParameter = isParameter;
                ParameterIndex = parameterIndex;
            }

            public string Value { get; }

            public bool IsParameter { get; }

            public int ParameterIndex { get; }

            public static RouteSegment Parse(string segment, List<string> parameterNames)
            {
                if (segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
                {
                    var parameterName = segment[1..^1];
                    return new RouteSegment(parameterName, true, GetOrAddParameterIndex(parameterNames, parameterName));
                }

                return new RouteSegment(segment, false, -1);
            }

            public bool Matches(string value)
            {
                return IsParameter || string.Equals(Value, value, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Read-only route value dictionary backed by small arrays instead of a hash table.
    /// </summary>
    internal sealed class CoapRouteValueCollection : IReadOnlyDictionary<string, string>
    {
        internal static readonly CoapRouteValueCollection Empty =
            new CoapRouteValueCollection(Array.Empty<string>(), Array.Empty<string>());

        private readonly string[] _keys;
        private readonly string[] _values;

        internal CoapRouteValueCollection(string[] keys, string[] values)
        {
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _values = values ?? throw new ArgumentNullException(nameof(values));
            if (_keys.Length != _values.Length)
            {
                throw new ArgumentException("Route value key and value arrays must have the same length.", nameof(values));
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> Keys => EnumerateKeys();

        /// <inheritdoc />
        public IEnumerable<string> Values => EnumerateValues();

        /// <inheritdoc />
        public int Count => _keys.Length;

        /// <inheritdoc />
        public string this[string key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException("The route value was not found.");
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return IndexOfKey(key) >= 0;
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, out string value)
        {
            var index = IndexOfKey(key);
            if (index >= 0)
            {
                value = _values[index];
                return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                yield return new KeyValuePair<string, string>(_keys[i], _values[i]);
            }
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private IEnumerable<string> EnumerateKeys()
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                yield return _keys[i];
            }
        }

        private IEnumerable<string> EnumerateValues()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                yield return _values[i];
            }
        }

        private int IndexOfKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            for (var i = 0; i < _keys.Length; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
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
            int accept)
        {
            Route = route;
            Method = method;
            PathSegments = pathSegments;
            RouteValues = routeValues;
            Queries = queries;
            Payload = payload;
            ContentFormat = contentFormat;
            Accept = accept;
        }

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
    }

    /// <summary>
    /// CoAP route handler response.
    /// </summary>
    public sealed class CoapRouteResult
    {
        private CoapRouteResult(
            StatusCode statusCode,
            ReadOnlyMemory<byte> payload,
            int contentFormat,
            IReadOnlyList<byte[]> eTags,
            long? maxAge,
            string locationPath,
            string locationQuery)
        {
            StatusCode = statusCode;
            Payload = payload;
            ContentFormat = contentFormat;
            ETags = eTags ?? Array.Empty<byte[]>();
            MaxAge = maxAge;
            LocationPath = locationPath;
            LocationQuery = locationQuery;
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

        /// <summary>
        /// Creates a response without a payload.
        /// </summary>
        /// <param name="statusCode">The CoAP response status code.</param>
        /// <returns>A route result.</returns>
        public static CoapRouteResult Status(StatusCode statusCode)
        {
            return new CoapRouteResult(statusCode, ReadOnlyMemory<byte>.Empty, MediaType.Undefined, null, null, null, null);
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
            return new CoapRouteResult(statusCode, payload, contentFormat, null, null, null, null);
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

        private CoapRouteResult Copy(
            IReadOnlyList<byte[]> eTags = null,
            long? maxAge = null,
            string locationPath = null,
            string locationQuery = null)
        {
            return new CoapRouteResult(
                StatusCode,
                Payload,
                ContentFormat,
                eTags ?? ETags,
                maxAge ?? MaxAge,
                locationPath ?? LocationPath,
                locationQuery ?? LocationQuery);
        }
    }

    /// <summary>
    /// Adapts route templates to the CoAP.NET resource tree.
    /// </summary>
    public sealed class CoapRouteEndpoint : IResource
    {
        private static readonly ILogger Log = CoapLogging.CreateLogger(typeof(CoapRouteEndpoint));
        private readonly IReadOnlyList<CoapRoute> _routes;
        private readonly IReadOnlyList<string> _pathSegments;
        private IResource _parent;

        private CoapRouteEndpoint(string name, IReadOnlyList<CoapRoute> routes, IReadOnlyList<string> pathSegments, bool visible)
        {
            Name = name;
            _routes = routes;
            _pathSegments = pathSegments;
            Visible = visible;
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

            return routes
                .GroupBy(route => route.RootSegment, StringComparer.Ordinal)
                .Select(group => (IResource)new CoapRouteEndpoint(group.Key, group.ToArray(), new[] { group.Key }, true))
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
        public bool Observable => false;

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
        public ResourceAttributes Attributes { get; } = new ResourceAttributes();

        /// <inheritdoc />
        public IEnumerable<IResource> Children => Array.Empty<IResource>();

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
            var matchingRoutes = _routes.Where(route => route.IsPrefix(nextSegments)).ToArray();
            return matchingRoutes.Length == 0
                ? null
                : new CoapRouteEndpoint(name, matchingRoutes, nextSegments, false) { Parent = this };
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
                HandleRouteRequestAsync(exchange).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "CoAP route handler failed. Path={Path}", string.Join("/", _pathSegments));
                SendText(exchange, StatusCode.InternalServerError, "CoAP route handler failed.");
            }
        }

        private async ValueTask HandleRouteRequestAsync(Exchange exchange)
        {
            var request = exchange.Request;
            foreach (var route in _routes)
            {
                if (!route.TryMatch(request.Method, _pathSegments, out var routeValues))
                {
                    continue;
                }

                var context = new CoapRouteContext(
                    route,
                    request.Method,
                    _pathSegments,
                    routeValues,
                    GetUriQueries(request),
                    request.Payload == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(request.Payload),
                    request.ContentFormat,
                    request.Accept);
                var result = await route.InvokeAsync(context).ConfigureAwait(false);
                SendResult(exchange, result ?? CoapRouteResult.Status(StatusCode.InternalServerError));
                return;
            }

            SendText(exchange, StatusCode.MethodNotAllowed, "CoAP route method is not allowed.");
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

        private static void SendResult(Exchange exchange, CoapRouteResult result)
        {
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

            foreach (var eTag in result.ETags)
            {
                response.AddETag(eTag);
            }

            exchange.SendResponse(response);
        }

        private static void SendText(Exchange exchange, StatusCode statusCode, string message)
        {
            var response = new Response(statusCode);
            response.SetPayload(message, MediaType.TextPlain);
            exchange.SendResponse(response);
        }
    }
}
