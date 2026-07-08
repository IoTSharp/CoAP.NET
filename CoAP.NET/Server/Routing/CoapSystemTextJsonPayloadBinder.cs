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
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Default JSON payload binder backed by <see cref="System.Text.Json"/>.
    /// </summary>
    public sealed class CoapSystemTextJsonPayloadBinder : ICoapJsonPayloadBinder
    {
        /// <summary>
        /// Creates a reflection-based binder with Web defaults.
        /// </summary>
        [RequiresUnreferencedCode("The default System.Text.Json resolver uses reflection. Native AOT hosts should register a source-generated CoapJsonTypeInfoPayloadBinder.")]
        [RequiresDynamicCode("The default System.Text.Json resolver may require runtime code generation. Native AOT hosts should register a source-generated CoapJsonTypeInfoPayloadBinder.")]
        public CoapSystemTextJsonPayloadBinder()
            : this(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            })
        {
        }

        /// <summary>
        /// Creates a binder with caller-provided serializer options.
        /// </summary>
        /// <param name="jsonSerializerOptions">Serializer options used to resolve JSON metadata.</param>
        [RequiresUnreferencedCode("Caller-provided JsonSerializerOptions may use reflection-based metadata. Native AOT hosts should register a source-generated CoapJsonTypeInfoPayloadBinder.")]
        [RequiresDynamicCode("Caller-provided JsonSerializerOptions may require runtime code generation. Native AOT hosts should register a source-generated CoapJsonTypeInfoPayloadBinder.")]
        public CoapSystemTextJsonPayloadBinder(JsonSerializerOptions jsonSerializerOptions)
        {
            JsonSerializerOptions = jsonSerializerOptions ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
        }

        /// <summary>
        /// Gets the serializer options used by this binder.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; }

        /// <inheritdoc />
        public object Bind(Type modelType, CoapRouteContext context)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            JsonTypeInfo jsonTypeInfo = JsonSerializerOptions.GetTypeInfo(modelType);
            return JsonSerializer.Deserialize(context.Payload.Span, jsonTypeInfo);
        }
    }
}
