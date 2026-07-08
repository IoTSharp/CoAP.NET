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
        internal static readonly CoapSystemTextJsonPayloadBinder Shared = new CoapSystemTextJsonPayloadBinder();

        /// <summary>
        /// Creates a binder with Web defaults.
        /// </summary>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026",
            Justification = "The default binder is reflection-based for non-AOT hosts; trimmed/AOT hosts can replace ICoapJsonPayloadBinder with a source-generated binder.")]
        [UnconditionalSuppressMessage(
            "AOT",
            "IL3050",
            Justification = "The default binder is reflection-based for non-AOT hosts; Native AOT hosts can replace ICoapJsonPayloadBinder with a source-generated binder.")]
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
