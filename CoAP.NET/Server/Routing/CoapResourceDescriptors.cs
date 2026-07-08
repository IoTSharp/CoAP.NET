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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Describes a CoAP resource class discovered from application parts.
    /// </summary>
    public sealed class CoapResourceDescriptor
    {
        /// <summary>
        /// Creates a resource descriptor.
        /// </summary>
        /// <param name="resourceType">The resource class type.</param>
        /// <param name="routePrefixes">Class-level route prefixes.</param>
        /// <param name="metadata">Metadata declared on the resource class.</param>
        public CoapResourceDescriptor(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
                DynamicallyAccessedMemberTypes.PublicMethods)]
            Type resourceType,
            IReadOnlyList<CoapRouteAttribute> routePrefixes,
            IReadOnlyList<object> metadata)
        {
            ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
            RoutePrefixes = routePrefixes ?? Array.Empty<CoapRouteAttribute>();
            Metadata = metadata ?? Array.Empty<object>();
        }

        /// <summary>
        /// Gets the resource class type.
        /// </summary>
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicMethods)]
        public Type ResourceType { get; }

        /// <summary>
        /// Gets route prefixes declared on the resource class.
        /// </summary>
        public IReadOnlyList<CoapRouteAttribute> RoutePrefixes { get; }

        /// <summary>
        /// Gets metadata declared on the resource class.
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }
    }

    /// <summary>
    /// Describes a routable CoAP resource action.
    /// </summary>
    public sealed class CoapResourceActionDescriptor
    {
        /// <summary>
        /// Creates an action descriptor.
        /// </summary>
        /// <param name="resource">The containing resource descriptor.</param>
        /// <param name="methodInfo">The action method.</param>
        /// <param name="coapMethod">The CoAP request method.</param>
        /// <param name="routeTemplate">The combined URI path template.</param>
        /// <param name="methodRoute">The method route attribute.</param>
        /// <param name="parameters">The reflected action parameters.</param>
        /// <param name="returnType">The reflected return type.</param>
        /// <param name="metadata">Endpoint metadata in declaration order.</param>
        public CoapResourceActionDescriptor(
            CoapResourceDescriptor resource,
            MethodInfo methodInfo,
            Method coapMethod,
            string routeTemplate,
            CoapMethodAttribute methodRoute,
            IReadOnlyList<ParameterInfo> parameters,
            Type returnType,
            IReadOnlyList<object> metadata)
        {
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            CoapMethod = coapMethod;
            RouteTemplate = string.IsNullOrWhiteSpace(routeTemplate)
                ? throw new ArgumentException("CoAP action route template is required.", nameof(routeTemplate))
                : routeTemplate;
            MethodRoute = methodRoute ?? throw new ArgumentNullException(nameof(methodRoute));
            Parameters = parameters ?? Array.Empty<ParameterInfo>();
            ReturnType = returnType ?? typeof(void);
            Metadata = metadata ?? Array.Empty<object>();
        }

        /// <summary>
        /// Gets the containing resource descriptor.
        /// </summary>
        public CoapResourceDescriptor Resource { get; }

        /// <summary>
        /// Gets the reflected action method.
        /// </summary>
        public MethodInfo MethodInfo { get; }

        /// <summary>
        /// Gets the CoAP request method handled by the action.
        /// </summary>
        public Method CoapMethod { get; }

        /// <summary>
        /// Gets the combined URI path template.
        /// </summary>
        public string RouteTemplate { get; }

        /// <summary>
        /// Gets the method route attribute that created the action endpoint.
        /// </summary>
        public CoapMethodAttribute MethodRoute { get; }

        /// <summary>
        /// Gets the reflected action parameters.
        /// </summary>
        public IReadOnlyList<ParameterInfo> Parameters { get; }

        /// <summary>
        /// Gets the reflected return type.
        /// </summary>
        public Type ReturnType { get; }

        /// <summary>
        /// Gets endpoint metadata in declaration order.
        /// </summary>
        public IReadOnlyList<object> Metadata { get; }

        /// <summary>
        /// Gets a diagnostic name for the action endpoint.
        /// </summary>
        public string DisplayName => Resource.ResourceType.FullName + "." + MethodInfo.Name + " (" + CoapMethod + " " + RouteTemplate + ")";
    }
}
