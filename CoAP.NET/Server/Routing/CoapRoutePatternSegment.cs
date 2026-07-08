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
    /// One segment in a parsed CoAP route pattern.
    /// </summary>
    public sealed class CoapRoutePatternSegment
    {
        private CoapRoutePatternSegment(
            string rawText,
            string literal,
            string parameterName,
            string constraint,
            int parameterIndex)
        {
            RawText = rawText;
            Literal = literal;
            ParameterName = parameterName;
            Constraint = constraint;
            ParameterIndex = parameterIndex;
        }

        /// <summary>
        /// Gets the original segment text from the normalized route template.
        /// </summary>
        public string RawText { get; }

        /// <summary>
        /// Gets the literal value when this segment is not a parameter.
        /// </summary>
        public string Literal { get; }

        /// <summary>
        /// Gets the route parameter name when this segment is a parameter.
        /// </summary>
        public string ParameterName { get; }

        /// <summary>
        /// Gets the optional parameter constraint text, such as <c>int</c> in <c>{id:int}</c>.
        /// </summary>
        public string Constraint { get; }

        /// <summary>
        /// Gets whether this segment is a route parameter.
        /// </summary>
        public bool IsParameter => ParameterName != null;

        internal int ParameterIndex { get; }

        internal static CoapRoutePatternSegment Parse(string segment, List<string> parameterNames)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            if (parameterNames == null)
            {
                throw new ArgumentNullException(nameof(parameterNames));
            }

            if (segment.Length > 2 && segment[0] == '{' && segment[segment.Length - 1] == '}')
            {
                var parameterText = segment.Substring(1, segment.Length - 2);
                var separator = parameterText.IndexOf(':');
                var parameterName = separator < 0
                    ? parameterText
                    : parameterText.Substring(0, separator);
                var constraint = separator < 0 || separator == parameterText.Length - 1
                    ? null
                    : parameterText.Substring(separator + 1);
                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    throw new ArgumentException("Route parameter name is required.", nameof(segment));
                }

                return new CoapRoutePatternSegment(
                    segment,
                    null,
                    parameterName,
                    constraint,
                    CoapRoutePattern.GetParameterIndex(parameterNames, parameterName));
            }

            return new CoapRoutePatternSegment(segment, segment, null, null, -1);
        }

        internal bool Matches(string value)
        {
            return IsParameter || string.Equals(Literal, value, StringComparison.Ordinal);
        }
    }
}
