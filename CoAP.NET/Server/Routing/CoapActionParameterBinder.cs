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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace CoAP.Server.Routing
{
    internal static class CoapActionParameterBinder
    {
        private static readonly ICoapJsonPayloadBinder MissingJsonPayloadBinder = new CoapMissingJsonPayloadBinder();

        [RequiresUnreferencedCode("Reflection-based CoAP action parameter binding reads runtime resource signatures. Native AOT hosts should use generated endpoints.")]
        [RequiresDynamicCode("Reflection-based CoAP action parameter binding may construct collection types dynamically. Native AOT hosts should use generated endpoints.")]
        public static object[] BindArguments(
            CoapResourceActionDescriptor descriptor,
            CoapRouteContext context)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (descriptor.Parameters.Count == 0)
            {
                return Array.Empty<object>();
            }

            var arguments = new object[descriptor.Parameters.Count];
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                arguments[i] = BindArgument(descriptor.Parameters[i], context);
            }

            return arguments;
        }

        [RequiresDynamicCode("Reflection-based CoAP parameter binding may construct collection values dynamically. Native AOT hosts should use generated endpoints.")]
        private static object BindArgument(ParameterInfo parameter, CoapRouteContext context)
        {
            var parameterType = parameter.ParameterType;
            if (parameterType == typeof(CoapRouteContext))
            {
                return context;
            }

            if (parameterType == typeof(CancellationToken))
            {
                return CancellationToken.None;
            }

            if (typeof(System.Net.EndPoint).IsAssignableFrom(parameterType))
            {
                if (context.RemoteEndPoint == null)
                {
                    return GetMissingValue(parameter, "remote endpoint");
                }

                if (!parameterType.IsInstanceOfType(context.RemoteEndPoint))
                {
                    throw new CoapBadRequestException(
                        "Remote endpoint cannot be bound to parameter '" + parameter.Name + "'.");
                }

                return context.RemoteEndPoint;
            }

            var payloadAttribute = parameter.GetCustomAttribute<CoapFromPayloadAttribute>(inherit: true);
            if (payloadAttribute != null)
            {
                return BindPayload(parameter, context);
            }

            var optionAttribute = parameter.GetCustomAttribute<CoapFromOptionAttribute>(inherit: true);
            if (optionAttribute != null)
            {
                return BindOption(parameter, context, optionAttribute.OptionType);
            }

            var routeAttribute = parameter.GetCustomAttribute<CoapFromRouteAttribute>(inherit: true);
            if (routeAttribute != null)
            {
                var name = GetBindingName(parameter, routeAttribute.Name);
                return TryBindRouteValue(parameter, context, name, out var routeValue)
                    ? routeValue
                    : GetMissingValue(parameter, "route value");
            }

            var queryAttribute = parameter.GetCustomAttribute<CoapFromQueryAttribute>(inherit: true);
            if (queryAttribute != null)
            {
                var name = GetBindingName(parameter, queryAttribute.Name);
                return TryBindQueryValue(parameter, context, name, out var queryValue)
                    ? queryValue
                    : GetMissingValue(parameter, "query value");
            }

            if (IsRawPayloadType(parameterType) || parameterType == typeof(JsonDocument))
            {
                return BindPayload(parameter, context);
            }

            if (!string.IsNullOrEmpty(parameter.Name))
            {
                if (TryBindRouteValue(parameter, context, parameter.Name, out var routeValue))
                {
                    return routeValue;
                }

                if (TryBindQueryValue(parameter, context, parameter.Name, out var queryValue))
                {
                    return queryValue;
                }

                if (TryBindWellKnownOption(parameter, context, out var optionValue))
                {
                    return optionValue;
                }
            }

            if (!IsSimpleBindableType(parameterType))
            {
                return BindPayload(parameter, context);
            }

            return GetMissingValue(parameter, "action parameter");
        }

        private static bool TryBindRouteValue(
            ParameterInfo parameter,
            CoapRouteContext context,
            string name,
            out object value)
        {
            value = null;
            if (context.RouteValues == null ||
                !context.RouteValues.TryGetValue(name, out var routeValue))
            {
                return false;
            }

            value = ConvertStringValue(routeValue, parameter.ParameterType, parameter.Name, "route value");
            return true;
        }

        [RequiresDynamicCode("Reflection-based CoAP query binding may construct collection values dynamically. Native AOT hosts should use generated endpoints.")]
        private static bool TryBindQueryValue(
            ParameterInfo parameter,
            CoapRouteContext context,
            string name,
            out object value)
        {
            var values = GetQueryValues(context.Queries, name);
            if (values.Count == 0)
            {
                value = null;
                return false;
            }

            value = ConvertStringValues(values, parameter.ParameterType, parameter.Name, "query value");
            return true;
        }

        [RequiresDynamicCode("Reflection-based CoAP payload binding may need default values for runtime-discovered collection parameters. Native AOT hosts should use generated endpoints.")]
        private static object BindPayload(ParameterInfo parameter, CoapRouteContext context)
        {
            var parameterType = parameter.ParameterType;
            if (parameterType == typeof(ReadOnlyMemory<byte>))
            {
                return context.Payload;
            }

            if (parameterType == typeof(byte[]))
            {
                return context.Payload.ToArray();
            }

            if (typeof(Stream).IsAssignableFrom(parameterType))
            {
                return new MemoryStream(context.Payload.ToArray(), writable: false);
            }

            if (parameterType == typeof(string))
            {
                return Encoding.UTF8.GetString(context.Payload.Span);
            }

            if (context.Payload.IsEmpty)
            {
                return GetMissingValue(parameter, "payload");
            }

            EnsureJsonContentFormat(context);
            try
            {
                if (parameterType == typeof(JsonDocument))
                {
                    return JsonDocument.Parse(context.Payload);
                }

                return GetJsonPayloadBinder(context).Bind(parameterType, context);
            }
            catch (JsonException ex)
            {
                throw new CoapBadRequestException(
                    "CoAP JSON payload cannot be bound to parameter '" + parameter.Name + "'.",
                    ex);
            }
        }

        [RequiresDynamicCode("Reflection-based CoAP option binding may construct collection values dynamically. Native AOT hosts should use generated endpoints.")]
        private static object BindOption(
            ParameterInfo parameter,
            CoapRouteContext context,
            OptionType optionType)
        {
            switch (optionType)
            {
                case OptionType.ContentFormat:
                    return ConvertIntegralValue(context.ContentFormat, parameter.ParameterType, parameter.Name, "Content-Format option");
                case OptionType.Accept:
                    return ConvertIntegralValue(context.Accept, parameter.ParameterType, parameter.Name, "Accept option");
                case OptionType.Observe:
                    return context.Observe.HasValue
                        ? ConvertIntegralValue(context.Observe.Value, parameter.ParameterType, parameter.Name, "Observe option")
                        : GetMissingValue(parameter, "Observe option");
                case OptionType.Block1:
                    return BindBlockOption(parameter, context.Block1, "Block1 option");
                case OptionType.Block2:
                    return BindBlockOption(parameter, context.Block2, "Block2 option");
                default:
                    return BindGeneralOption(parameter, context, optionType);
            }
        }

        [RequiresDynamicCode("Reflection-based CoAP option binding may construct collection values dynamically. Native AOT hosts should use generated endpoints.")]
        private static bool TryBindWellKnownOption(
            ParameterInfo parameter,
            CoapRouteContext context,
            out object value)
        {
            value = null;
            var name = parameter.Name;
            if (string.Equals(name, "contentFormat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "contentType", StringComparison.OrdinalIgnoreCase))
            {
                value = ConvertIntegralValue(context.ContentFormat, parameter.ParameterType, name, "Content-Format option");
                return true;
            }

            if (string.Equals(name, "accept", StringComparison.OrdinalIgnoreCase))
            {
                value = ConvertIntegralValue(context.Accept, parameter.ParameterType, name, "Accept option");
                return true;
            }

            if (string.Equals(name, "observe", StringComparison.OrdinalIgnoreCase))
            {
                value = context.Observe.HasValue
                    ? ConvertIntegralValue(context.Observe.Value, parameter.ParameterType, name, "Observe option")
                    : GetMissingValue(parameter, "Observe option");
                return true;
            }

            if (string.Equals(name, "block1", StringComparison.OrdinalIgnoreCase))
            {
                value = BindBlockOption(parameter, context.Block1, "Block1 option");
                return true;
            }

            if (string.Equals(name, "block2", StringComparison.OrdinalIgnoreCase))
            {
                value = BindBlockOption(parameter, context.Block2, "Block2 option");
                return true;
            }

            if (string.Equals(name, "etag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "eTag", StringComparison.Ordinal))
            {
                value = BindGeneralOption(parameter, context, OptionType.ETag);
                return true;
            }

            if (string.Equals(name, "etags", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "eTags", StringComparison.Ordinal))
            {
                value = BindGeneralOption(parameter, context, OptionType.ETag);
                return true;
            }

            if (string.Equals(name, "token", StringComparison.OrdinalIgnoreCase))
            {
                value = BindToken(parameter, context);
                return true;
            }

            return false;
        }

        [RequiresDynamicCode("Reflection-based CoAP option binding may need default values for runtime-discovered collection parameters. Native AOT hosts should use generated endpoints.")]
        private static object BindBlockOption(ParameterInfo parameter, BlockOption blockOption, string source)
        {
            if (blockOption == null)
            {
                return GetMissingValue(parameter, source);
            }

            if (parameter.ParameterType == typeof(BlockOption) ||
                parameter.ParameterType.IsAssignableFrom(typeof(BlockOption)))
            {
                return blockOption;
            }

            return ConvertIntegralValue(blockOption.IntValue, parameter.ParameterType, parameter.Name, source);
        }

        [RequiresDynamicCode("Reflection-based CoAP option binding may construct collection values dynamically. Native AOT hosts should use generated endpoints.")]
        private static object BindGeneralOption(
            ParameterInfo parameter,
            CoapRouteContext context,
            OptionType optionType)
        {
            var options = GetOptions(context, optionType);
            if (TryGetCollectionElementType(parameter.ParameterType, out var elementType))
            {
                if (elementType == typeof(Option))
                {
                    return CreateCollection(parameter.ParameterType, elementType, options);
                }

                if (elementType == typeof(byte[]))
                {
                    var rawValues = new List<byte[]>(options.Count);
                    for (var i = 0; i < options.Count; i++)
                    {
                        rawValues.Add(CloneBytes(options[i].RawValue));
                    }

                    return CreateCollection(parameter.ParameterType, elementType, rawValues);
                }
            }

            if (options.Count == 0)
            {
                return GetMissingValue(parameter, Option.ToString(optionType) + " option");
            }

            return ConvertOptionValue(options[0], parameter.ParameterType, parameter.Name);
        }

        [RequiresDynamicCode("Reflection-based CoAP token binding may need default values for runtime-discovered collection parameters. Native AOT hosts should use generated endpoints.")]
        private static object BindToken(ParameterInfo parameter, CoapRouteContext context)
        {
            if (context.Token == null || context.Token.Length == 0)
            {
                return GetMissingValue(parameter, "Token");
            }

            if (parameter.ParameterType == typeof(byte[]))
            {
                return CloneBytes(context.Token);
            }

            if (parameter.ParameterType == typeof(string))
            {
                return BitConverter.ToString(context.Token).Replace("-", string.Empty);
            }

            return GetMissingValue(parameter, "Token");
        }

        private static object ConvertOptionValue(Option option, Type targetType, string parameterName)
        {
            if (targetType == typeof(Option) || targetType.IsAssignableFrom(option.GetType()))
            {
                return option;
            }

            if (targetType == typeof(byte[]))
            {
                return CloneBytes(option.RawValue);
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType == typeof(string))
            {
                return option.StringValue;
            }

            if (targetType == typeof(int))
            {
                return option.IntValue;
            }

            if (targetType == typeof(long))
            {
                return option.LongValue;
            }

            if (targetType == typeof(bool))
            {
                return true;
            }

            return ConvertStringValue(
                option.Value == null ? null : Convert.ToString(option.Value, CultureInfo.InvariantCulture),
                targetType,
                parameterName,
                Option.ToString(option.Type) + " option");
        }

        private static object ConvertIntegralValue(
            int value,
            Type targetType,
            string parameterName,
            string source)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType == typeof(int))
            {
                return value;
            }

            if (targetType == typeof(long))
            {
                return (long)value;
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return ConvertStringValue(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                targetType,
                parameterName,
                source);
        }

        [RequiresDynamicCode("Reflection-based CoAP collection binding creates collection values for runtime-discovered element types. Native AOT hosts should use generated endpoints.")]
        private static object ConvertStringValues(
            IReadOnlyList<string> values,
            Type targetType,
            string parameterName,
            string source)
        {
            if (TryGetCollectionElementType(targetType, out var elementType))
            {
                var converted = new ArrayList(values.Count);
                for (var i = 0; i < values.Count; i++)
                {
                    converted.Add(ConvertStringValue(values[i], elementType, parameterName, source));
                }

                return CreateCollection(targetType, elementType, converted);
            }

            return ConvertStringValue(values[values.Count - 1], targetType, parameterName, source);
        }

        private static object ConvertStringValue(
            string value,
            Type targetType,
            string parameterName,
            string source)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                targetType = nullableType;
            }

            if (targetType == typeof(string))
            {
                return value;
            }

            try
            {
                if (targetType == typeof(Guid))
                {
                    return Guid.Parse(value);
                }

                if (targetType == typeof(Uri))
                {
                    return new Uri(value, UriKind.RelativeOrAbsolute);
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(DateTime))
                {
                    return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }

                if (targetType == typeof(TimeSpan))
                {
                    return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(bool))
                {
                    if (string.Equals(value, "1", StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (string.Equals(value, "0", StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, value, ignoreCase: true);
                }

                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new CoapBadRequestException(
                    "CoAP " + source + " for parameter '" + parameterName + "' cannot be converted to " +
                    targetType.FullName + ".",
                    ex);
            }
        }

        [RequiresDynamicCode("Reflection-based CoAP default value binding may create collections for runtime-discovered element types. Native AOT hosts should use generated endpoints.")]
        private static object GetMissingValue(ParameterInfo parameter, string source)
        {
            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            var targetType = parameter.ParameterType;
            if (TryGetCollectionElementType(targetType, out var elementType))
            {
                return CreateCollection(targetType, elementType, Array.Empty<object>());
            }

            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
            {
                return null;
            }

            throw new CoapBadRequestException(
                "Required CoAP " + source + " for parameter '" + parameter.Name + "' is missing.");
        }

        private static string GetBindingName(ParameterInfo parameter, string configuredName)
        {
            if (!string.IsNullOrWhiteSpace(configuredName))
            {
                return configuredName;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Name))
            {
                return parameter.Name;
            }

            throw new InvalidOperationException("A CoAP binding name is required for unnamed action parameters.");
        }

        private static void EnsureJsonContentFormat(CoapRouteContext context)
        {
            if (context.ContentFormat != MediaType.ApplicationJson)
            {
                throw new CoapUnsupportedMediaTypeException(
                    "CoAP JSON payload requires Content-Format " + MediaType.ToString(MediaType.ApplicationJson) + ".");
            }
        }

        private static ICoapJsonPayloadBinder GetJsonPayloadBinder(CoapRouteContext context)
        {
            return context.RequestServices?.GetService(typeof(ICoapJsonPayloadBinder)) as ICoapJsonPayloadBinder ??
                MissingJsonPayloadBinder;
        }

        private static bool IsRawPayloadType(Type type)
        {
            return type == typeof(ReadOnlyMemory<byte>) ||
                type == typeof(byte[]) ||
                typeof(Stream).IsAssignableFrom(type);
        }

        private static bool IsSimpleBindableType(Type type)
        {
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                type = nullableType;
            }

            return type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(Guid) ||
                type == typeof(Uri) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan);
        }

        private static IReadOnlyList<Option> GetOptions(CoapRouteContext context, OptionType optionType)
        {
            var options = new List<Option>();
            foreach (var option in context.GetOptions(optionType))
            {
                options.Add(option);
            }

            return options.Count == 0 ? Array.Empty<Option>() : options.ToArray();
        }

        private static IReadOnlyList<string> GetQueryValues(IReadOnlyList<string> queries, string name)
        {
            if (queries == null || queries.Count == 0)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            for (var i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                if (query == null)
                {
                    continue;
                }

                var separator = query.IndexOf('=');
                var key = separator < 0 ? query : query.Substring(0, separator);
                if (!string.Equals(DecodeQueryComponent(key), name, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = separator < 0 ? string.Empty : query.Substring(separator + 1);
                values.Add(DecodeQueryComponent(value));
            }

            return values.Count == 0 ? Array.Empty<string>() : values.ToArray();
        }

        private static string DecodeQueryComponent(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                return value;
            }
        }

        private static bool TryGetCollectionElementType(Type targetType, out Type elementType)
        {
            elementType = null;
            if (targetType == typeof(string) || targetType == typeof(byte[]))
            {
                return false;
            }

            if (targetType.IsArray)
            {
                elementType = targetType.GetElementType();
                return elementType != null;
            }

            if (targetType.IsGenericType)
            {
                var definition = targetType.GetGenericTypeDefinition();
                if (definition == typeof(IEnumerable<>) ||
                    definition == typeof(IReadOnlyList<>) ||
                    definition == typeof(ICollection<>) ||
                    definition == typeof(IList<>) ||
                    definition == typeof(List<>))
                {
                    elementType = targetType.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }

        [RequiresDynamicCode("Reflection-based CoAP collection binding creates arrays and List<T> instances for runtime-discovered element types. Native AOT hosts should use generated endpoints.")]
        private static object CreateCollection(
            Type targetType,
            Type elementType,
            IEnumerable values)
        {
            var arrayList = new ArrayList();
            foreach (var value in values)
            {
                arrayList.Add(value);
            }

            var array = Array.CreateInstance(elementType, arrayList.Count);
            for (var i = 0; i < arrayList.Count; i++)
            {
                array.SetValue(arrayList[i], i);
            }

            if (targetType.IsAssignableFrom(array.GetType()))
            {
                return array;
            }

            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);
            for (var i = 0; i < arrayList.Count; i++)
            {
                list.Add(arrayList[i]);
            }

            if (targetType.IsAssignableFrom(listType))
            {
                return list;
            }

            return array;
        }

        private static byte[] CloneBytes(byte[] value)
        {
            return value == null ? null : (byte[])value.Clone();
        }
    }
}
