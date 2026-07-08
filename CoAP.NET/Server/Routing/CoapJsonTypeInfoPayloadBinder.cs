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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Binds JSON payloads through source-generated <see cref="JsonTypeInfo"/> metadata.
    /// </summary>
    public sealed class CoapJsonTypeInfoPayloadBinder : ICoapJsonPayloadBinder
    {
        private readonly IJsonTypeInfoResolver _resolver;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        /// <summary>
        /// Creates a binder from a source-generated JSON serializer context.
        /// </summary>
        /// <param name="jsonSerializerContext">The source-generated JSON metadata context.</param>
        public CoapJsonTypeInfoPayloadBinder(JsonSerializerContext jsonSerializerContext)
            : this(jsonSerializerContext, jsonSerializerContext?.Options)
        {
        }

        /// <summary>
        /// Creates a binder from a JSON type info resolver.
        /// </summary>
        /// <param name="resolver">The source-generated JSON metadata resolver.</param>
        /// <param name="jsonSerializerOptions">Serializer options used when resolving metadata.</param>
        public CoapJsonTypeInfoPayloadBinder(
            IJsonTypeInfoResolver resolver,
            JsonSerializerOptions jsonSerializerOptions = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                TypeInfoResolver = resolver
            };
        }

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

            var jsonTypeInfo = _resolver.GetTypeInfo(modelType, _jsonSerializerOptions);
            if (jsonTypeInfo == null)
            {
                throw new CoapBadRequestException(
                    "No source-generated JSON metadata is registered for CoAP payload type '" +
                    modelType.FullName + "'.");
            }

            return JsonSerializer.Deserialize(context.Payload.Span, jsonTypeInfo);
        }
    }

    internal sealed class CoapMissingJsonPayloadBinder : ICoapJsonPayloadBinder
    {
        public object Bind(Type modelType, CoapRouteContext context)
        {
            if (modelType == null)
            {
                throw new ArgumentNullException(nameof(modelType));
            }

            throw new CoapBadRequestException(
                "No CoAP JSON payload binder is configured for type '" + modelType.FullName +
                "'. Register a source-generated JsonSerializerContext with AddCoapJsonPayloadBinder.");
        }
    }
}
