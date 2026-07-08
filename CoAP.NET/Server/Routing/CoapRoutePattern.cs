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

using System;
using System.Collections.Generic;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Parsed CoAP URI path pattern used by endpoint routing.
    /// </summary>
    public sealed class CoapRoutePattern
    {
        private readonly CoapRoutePatternSegment[] _segments;
        private readonly string[] _parameterNames;

        private CoapRoutePattern(
            string template,
            CoapRoutePatternSegment[] segments,
            string[] parameterNames)
        {
            Template = template;
            _segments = segments;
            _parameterNames = parameterNames;
        }

        /// <summary>
        /// Gets the normalized route template without leading or trailing slashes.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Gets the parsed path segments.
        /// </summary>
        public IReadOnlyList<CoapRoutePatternSegment> Segments => _segments;

        /// <summary>
        /// Gets the unique parameter names in first-seen order.
        /// </summary>
        public IReadOnlyList<string> ParameterNames => _parameterNames;

        /// <summary>
        /// Gets the first literal segment used to attach this pattern to the resource tree.
        /// </summary>
        public string RootSegment
        {
            get
            {
                if (_segments.Length == 0 || _segments[0].IsParameter)
                {
                    throw new InvalidOperationException("CoAP route pattern root segment must be a literal resource name.");
                }

                return _segments[0].Literal;
            }
        }

        /// <summary>
        /// Parses a URI path template into a route pattern.
        /// </summary>
        /// <param name="template">The URI path template, such as diagnostics/{target}/status.</param>
        /// <returns>A parsed route pattern.</returns>
        public static CoapRoutePattern Parse(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Route template is required.", nameof(template));
            }

            var normalizedTemplate = template.Trim('/');
            var rawSegments = normalizedTemplate.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawSegments.Length == 0)
            {
                throw new ArgumentException("Route template must contain at least one segment.", nameof(template));
            }

            var parameterNames = new List<string>();
            var segments = new CoapRoutePatternSegment[rawSegments.Length];
            for (var i = 0; i < rawSegments.Length; i++)
            {
                segments[i] = CoapRoutePatternSegment.Parse(rawSegments[i], parameterNames);
            }

            if (segments[0].IsParameter)
            {
                throw new ArgumentException("Route template root segment must be a literal resource name.", nameof(template));
            }

            return new CoapRoutePattern(
                normalizedTemplate,
                segments,
                parameterNames.Count == 0 ? Array.Empty<string>() : parameterNames.ToArray());
        }

        /// <summary>
        /// Checks whether the supplied URI path is a prefix of this pattern.
        /// </summary>
        /// <param name="pathSegments">The current URI path segments.</param>
        /// <returns><c>true</c> when the path can still reach this route pattern.</returns>
        public bool IsPrefix(IReadOnlyList<string> pathSegments)
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

        /// <summary>
        /// Matches a complete URI path and extracts route values.
        /// </summary>
        /// <param name="pathSegments">The complete URI path segments.</param>
        /// <param name="routeValues">Values extracted from parameter segments.</param>
        /// <returns><c>true</c> when the URI path matches this pattern.</returns>
        public bool TryMatch(
            IReadOnlyList<string> pathSegments,
            out IReadOnlyDictionary<string, string> routeValues)
        {
            routeValues = null;
            if (pathSegments == null || pathSegments.Count != _segments.Length)
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

        internal static int GetParameterIndex(List<string> parameterNames, string parameterName)
        {
            return GetOrAddParameterIndex(parameterNames, parameterName);
        }
    }
}
