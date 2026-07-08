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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Marks a class as a CoAP resource discovered by <c>AddCoapResources()</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CoapResourceAttribute : Attribute
    {
    }

    /// <summary>
    /// Compatibility marker for MVC-style controller naming. Prefer <see cref="CoapResourceAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class CoapControllerAttribute : Attribute
    {
    }

    /// <summary>
    /// Defines a CoAP URI path template on a resource class or action method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class CoapRouteAttribute : Attribute
    {
        /// <summary>
        /// Creates a route attribute.
        /// </summary>
        /// <param name="template">The URI path template or prefix.</param>
        public CoapRouteAttribute(string template)
        {
            Template = template ?? string.Empty;
        }

        /// <summary>
        /// Gets the route template without interpretation.
        /// </summary>
        public string Template { get; }
    }

    /// <summary>
    /// Base class for CoAP method route attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class CoapMethodAttribute : CoapRouteAttribute
    {
        /// <summary>
        /// Creates a method route attribute.
        /// </summary>
        /// <param name="method">The CoAP request method.</param>
        /// <param name="template">The action URI path template.</param>
        protected CoapMethodAttribute(Method method, string template)
            : base(template)
        {
            Method = method;
        }

        /// <summary>
        /// Gets the CoAP request method handled by the action.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Gets whether the action declares Observe semantics.
        /// </summary>
        public virtual bool IsObserve => false;
    }

    /// <summary>
    /// Maps a resource action to CoAP GET.
    /// </summary>
    public sealed class CoapGetAttribute : CoapMethodAttribute
    {
        /// <summary>
        /// Creates a GET route attribute.
        /// </summary>
        /// <param name="template">The action URI path template.</param>
        public CoapGetAttribute(string template = "")
            : base(Method.GET, template)
        {
        }
    }

    /// <summary>
    /// Maps a resource action to CoAP POST.
    /// </summary>
    public sealed class CoapPostAttribute : CoapMethodAttribute
    {
        /// <summary>
        /// Creates a POST route attribute.
        /// </summary>
        /// <param name="template">The action URI path template.</param>
        public CoapPostAttribute(string template = "")
            : base(Method.POST, template)
        {
        }
    }

    /// <summary>
    /// Maps a resource action to CoAP PUT.
    /// </summary>
    public sealed class CoapPutAttribute : CoapMethodAttribute
    {
        /// <summary>
        /// Creates a PUT route attribute.
        /// </summary>
        /// <param name="template">The action URI path template.</param>
        public CoapPutAttribute(string template = "")
            : base(Method.PUT, template)
        {
        }
    }

    /// <summary>
    /// Maps a resource action to CoAP DELETE.
    /// </summary>
    public sealed class CoapDeleteAttribute : CoapMethodAttribute
    {
        /// <summary>
        /// Creates a DELETE route attribute.
        /// </summary>
        /// <param name="template">The action URI path template.</param>
        public CoapDeleteAttribute(string template = "")
            : base(Method.DELETE, template)
        {
        }
    }

    /// <summary>
    /// Maps a resource action to an observable CoAP GET.
    /// </summary>
    public sealed class CoapObserveAttribute : CoapMethodAttribute
    {
        /// <summary>
        /// Creates an Observe route attribute.
        /// </summary>
        /// <param name="template">The action URI path template.</param>
        public CoapObserveAttribute(string template = "")
            : base(Method.GET, template)
        {
        }

        /// <inheritdoc />
        public override bool IsObserve => true;
    }

    /// <summary>
    /// Adds a human-readable title metadata item for future resource discovery output.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class CoapResourceTitleAttribute : Attribute
    {
        /// <summary>
        /// Creates title metadata.
        /// </summary>
        /// <param name="title">The resource title.</param>
        public CoapResourceTitleAttribute(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("CoAP resource title is required.", nameof(title));
            }

            Title = title;
        }

        /// <summary>
        /// Gets the resource title.
        /// </summary>
        public string Title { get; }
    }
}
