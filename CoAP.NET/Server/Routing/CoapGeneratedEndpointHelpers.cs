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
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// 为 source generator 生成的 CoAP endpoint 提供运行时辅助能力。
    /// </summary>
    public static class CoapGeneratedEndpointHelpers
    {
        private static readonly ICoapJsonPayloadBinder MissingJsonPayloadBinder = new CoapMissingJsonPayloadBinder();

        /// <summary>
        /// 在需要时把当前请求上下文挂到 <see cref="CoapResourceBase"/>，然后执行生成的 action 委托。
        /// </summary>
        /// <typeparam name="TResource">CoAP resource 类型。</typeparam>
        /// <param name="resource">本次请求创建的 resource 实例。</param>
        /// <param name="context">当前 CoAP 路由上下文。</param>
        /// <param name="action">生成代码构造的 action 调用。</param>
        /// <returns>标准 CoAP route 结果。</returns>
        public static async ValueTask<CoapRouteResult> InvokeWithContextAsync<TResource>(
            TResource resource,
            CoapRouteContext context,
            Func<ValueTask<CoapRouteResult>> action)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (resource is CoapResourceBase resourceBase)
            {
                return await resourceBase.InvokeWithContextAsync(context, action).ConfigureAwait(false);
            }

            return await action().ConfigureAwait(false);
        }

        /// <summary>
        /// 把 resource action 的返回值转换为 CoAP route 结果。
        /// </summary>
        /// <param name="result">action 返回值。</param>
        /// <returns>标准 CoAP route 结果。</returns>
        public static async ValueTask<CoapRouteResult> ConvertResultAsync(object result)
        {
            switch (result)
            {
                case null:
                    return CoapRouteResult.Changed();
                case CoapRouteResult routeResult:
                    return routeResult;
                case ICoapResult coapResult:
                    return CoapRouteResult.FromResult(coapResult);
                case string text:
                    return CoapRouteResult.Text(StatusCode.Content, text);
                case byte[] bytes:
                    return CoapRouteResult.Content(bytes, MediaType.ApplicationOctetStream);
                case Task<CoapRouteResult> routeResultTask:
                    return await routeResultTask.ConfigureAwait(false);
                case Task<ICoapResult> coapResultTask:
                    return CoapRouteResult.FromResult(await coapResultTask.ConfigureAwait(false));
                case ValueTask<CoapRouteResult> routeResultValueTask:
                    return await routeResultValueTask.ConfigureAwait(false);
                case ValueTask<ICoapResult> coapResultValueTask:
                    return CoapRouteResult.FromResult(await coapResultValueTask.ConfigureAwait(false));
                case Task task:
                    await task.ConfigureAwait(false);
                    return CoapRouteResult.Changed();
                case ValueTask valueTask:
                    await valueTask.ConfigureAwait(false);
                    return CoapRouteResult.Changed();
                default:
                    throw new InvalidOperationException(
                        "Unsupported CoAP resource action return type '" + result.GetType().FullName + "'.");
            }
        }

        /// <summary>
        /// 从 route values 绑定 action 参数。
        /// </summary>
        public static T BindRouteValue<T>(
            CoapRouteContext context,
            string name,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (TryBindRouteValue(context, name, parameterName, out T value))
            {
                return value;
            }

            return GetMissingValue(parameterName, "route value", hasDefaultValue, defaultValue);
        }

        /// <summary>
        /// 从 Uri-Query 绑定标量 action 参数。
        /// </summary>
        public static T BindQueryValue<T>(
            CoapRouteContext context,
            string name,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            var values = GetQueryValues(context == null ? null : context.Queries, name);
            if (values.Count == 0)
            {
                return GetMissingValue(parameterName, "query value", hasDefaultValue, defaultValue);
            }

            return ConvertStringValue<T>(values[values.Count - 1], parameterName, "query value");
        }

        /// <summary>
        /// 从 Uri-Query 绑定数组 action 参数。
        /// </summary>
        public static TElement[] BindQueryArray<TElement>(
            CoapRouteContext context,
            string name,
            string parameterName,
            bool hasDefaultValue,
            TElement[] defaultValue)
        {
            var values = GetQueryValues(context == null ? null : context.Queries, name);
            if (values.Count == 0)
            {
                return hasDefaultValue ? defaultValue : Array.Empty<TElement>();
            }

            var result = new TElement[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                result[i] = ConvertStringValue<TElement>(values[i], parameterName, "query value");
            }

            return result;
        }

        /// <summary>
        /// 从 Uri-Query 绑定列表 action 参数。
        /// </summary>
        public static List<TElement> BindQueryList<TElement>(
            CoapRouteContext context,
            string name,
            string parameterName,
            bool hasDefaultValue,
            List<TElement> defaultValue)
        {
            var values = GetQueryValues(context == null ? null : context.Queries, name);
            if (values.Count == 0)
            {
                return hasDefaultValue ? defaultValue : new List<TElement>();
            }

            var result = new List<TElement>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                result.Add(ConvertStringValue<TElement>(values[i], parameterName, "query value"));
            }

            return result;
        }

        /// <summary>
        /// 按 CoAP.NET 的默认推断规则绑定 action 参数。
        /// </summary>
        public static T BindInferred<T>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (TryBindRouteValue(context, parameterName, parameterName, out T routeValue))
            {
                return routeValue;
            }

            var values = GetQueryValues(context == null ? null : context.Queries, parameterName);
            if (values.Count > 0)
            {
                return ConvertStringValue<T>(values[values.Count - 1], parameterName, "query value");
            }

            if (TryBindWellKnownOption(context, parameterName, out T optionValue))
            {
                return optionValue;
            }

            var targetType = typeof(T);
            if (IsRawPayloadType(targetType) ||
                targetType == typeof(JsonDocument) ||
                !IsSimpleBindableType(targetType))
            {
                return BindPayload(context, parameterName, hasDefaultValue, defaultValue);
            }

            return GetMissingValue(parameterName, "action parameter", hasDefaultValue, defaultValue);
        }

        /// <summary>
        /// 按默认推断规则从 Uri-Query 绑定数组参数。
        /// </summary>
        public static TElement[] BindInferredArray<TElement>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            TElement[] defaultValue)
        {
            return BindQueryArray(context, parameterName, parameterName, hasDefaultValue, defaultValue);
        }

        /// <summary>
        /// 按默认推断规则从 Uri-Query 绑定列表参数。
        /// </summary>
        public static List<TElement> BindInferredList<TElement>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            List<TElement> defaultValue)
        {
            return BindQueryList(context, parameterName, parameterName, hasDefaultValue, defaultValue);
        }

        /// <summary>
        /// 从请求 payload 绑定 action 参数。
        /// </summary>
        public static T BindPayload<T>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var parameterType = typeof(T);
            if (parameterType == typeof(ReadOnlyMemory<byte>))
            {
                return (T)(object)context.Payload;
            }

            if (parameterType == typeof(byte[]))
            {
                return (T)(object)context.Payload.ToArray();
            }

            if (typeof(Stream).IsAssignableFrom(parameterType))
            {
                return (T)(object)new MemoryStream(context.Payload.ToArray(), writable: false);
            }

            if (parameterType == typeof(string))
            {
                return (T)(object)Encoding.UTF8.GetString(context.Payload.Span);
            }

            if (context.Payload.IsEmpty)
            {
                return GetMissingValue(parameterName, "payload", hasDefaultValue, defaultValue);
            }

            EnsureJsonContentFormat(context);
            try
            {
                if (parameterType == typeof(JsonDocument))
                {
                    return (T)(object)JsonDocument.Parse(context.Payload);
                }

                return (T)GetJsonPayloadBinder(context).Bind(parameterType, context);
            }
            catch (JsonException ex)
            {
                throw new CoapBadRequestException(
                    "CoAP JSON payload cannot be bound to parameter '" + parameterName + "'.",
                    ex);
            }
        }

        /// <summary>
        /// 从指定 CoAP option 绑定 action 参数。
        /// </summary>
        public static T BindOption<T>(
            CoapRouteContext context,
            OptionType optionType,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            switch (optionType)
            {
                case OptionType.ContentFormat:
                    return ConvertIntegralValue<T>(context == null ? MediaType.Undefined : context.ContentFormat, parameterName, "Content-Format option");
                case OptionType.Accept:
                    return ConvertIntegralValue<T>(context == null ? MediaType.Undefined : context.Accept, parameterName, "Accept option");
                case OptionType.Observe:
                    return context != null && context.Observe.HasValue
                        ? ConvertIntegralValue<T>(context.Observe.Value, parameterName, "Observe option")
                        : GetMissingValue(parameterName, "Observe option", hasDefaultValue, defaultValue);
                case OptionType.Block1:
                    return BindBlockOption(context == null ? null : context.Block1, parameterName, "Block1 option", hasDefaultValue, defaultValue);
                case OptionType.Block2:
                    return BindBlockOption(context == null ? null : context.Block2, parameterName, "Block2 option", hasDefaultValue, defaultValue);
                default:
                    return BindGeneralOption(context, optionType, parameterName, hasDefaultValue, defaultValue);
            }
        }

        /// <summary>
        /// 绑定请求来源网络端点。
        /// </summary>
        public static T BindRemoteEndPoint<T>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (context == null || context.RemoteEndPoint == null)
            {
                return GetMissingValue(parameterName, "remote endpoint", hasDefaultValue, defaultValue);
            }

            if (context.RemoteEndPoint is T remoteEndPoint)
            {
                return remoteEndPoint;
            }

            throw new CoapBadRequestException(
                "Remote endpoint cannot be bound to parameter '" + parameterName + "'.");
        }

        private static bool TryBindRouteValue<T>(
            CoapRouteContext context,
            string name,
            string parameterName,
            out T value)
        {
            value = default;
            if (context == null ||
                context.RouteValues == null ||
                !context.RouteValues.TryGetValue(name, out var routeValue))
            {
                return false;
            }

            value = ConvertStringValue<T>(routeValue, parameterName, "route value");
            return true;
        }

        private static bool TryBindWellKnownOption<T>(
            CoapRouteContext context,
            string parameterName,
            out T value)
        {
            value = default;
            if (context == null)
            {
                return false;
            }

            if (string.Equals(parameterName, "contentFormat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameterName, "contentType", StringComparison.OrdinalIgnoreCase))
            {
                value = ConvertIntegralValue<T>(context.ContentFormat, parameterName, "Content-Format option");
                return true;
            }

            if (string.Equals(parameterName, "accept", StringComparison.OrdinalIgnoreCase))
            {
                value = ConvertIntegralValue<T>(context.Accept, parameterName, "Accept option");
                return true;
            }

            if (string.Equals(parameterName, "observe", StringComparison.OrdinalIgnoreCase))
            {
                if (!context.Observe.HasValue)
                {
                    return false;
                }

                value = ConvertIntegralValue<T>(context.Observe.Value, parameterName, "Observe option");
                return true;
            }

            if (string.Equals(parameterName, "block1", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Block1 == null)
                {
                    return false;
                }

                value = BindBlockOption(context.Block1, parameterName, "Block1 option", false, default(T));
                return true;
            }

            if (string.Equals(parameterName, "block2", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Block2 == null)
                {
                    return false;
                }

                value = BindBlockOption(context.Block2, parameterName, "Block2 option", false, default(T));
                return true;
            }

            if (string.Equals(parameterName, "etag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameterName, "eTag", StringComparison.Ordinal))
            {
                value = BindGeneralOption(context, OptionType.ETag, parameterName, false, default(T));
                return true;
            }

            if (string.Equals(parameterName, "token", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Token == null || context.Token.Length == 0)
                {
                    return false;
                }

                value = BindToken<T>(context, parameterName, false, default);
                return true;
            }

            return false;
        }

        private static T BindBlockOption<T>(
            BlockOption blockOption,
            string parameterName,
            string source,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (blockOption == null)
            {
                return GetMissingValue(parameterName, source, hasDefaultValue, defaultValue);
            }

            if (blockOption is T typedBlockOption)
            {
                return typedBlockOption;
            }

            return ConvertIntegralValue<T>(blockOption.IntValue, parameterName, source);
        }

        private static T BindGeneralOption<T>(
            CoapRouteContext context,
            OptionType optionType,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            var option = context == null ? null : context.GetFirstOption(optionType);
            if (option == null)
            {
                return GetMissingValue(parameterName, Option.ToString(optionType) + " option", hasDefaultValue, defaultValue);
            }

            return ConvertOptionValue<T>(option, parameterName);
        }

        private static T BindToken<T>(
            CoapRouteContext context,
            string parameterName,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (context == null || context.Token == null || context.Token.Length == 0)
            {
                return GetMissingValue(parameterName, "Token", hasDefaultValue, defaultValue);
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(byte[]))
            {
                return (T)(object)CloneBytes(context.Token);
            }

            if (targetType == typeof(string))
            {
                return (T)(object)BitConverter.ToString(context.Token).Replace("-", string.Empty);
            }

            return GetMissingValue(parameterName, "Token", hasDefaultValue, defaultValue);
        }

        private static T ConvertOptionValue<T>(Option option, string parameterName)
        {
            if (option is T typedOption)
            {
                return typedOption;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(byte[]))
            {
                return (T)(object)CloneBytes(option.RawValue);
            }

            if (targetType == typeof(string))
            {
                return (T)(object)option.StringValue;
            }

            if (targetType == typeof(int))
            {
                return (T)(object)option.IntValue;
            }

            if (targetType == typeof(long))
            {
                return (T)(object)option.LongValue;
            }

            if (targetType == typeof(bool))
            {
                return (T)(object)true;
            }

            return ConvertStringValue<T>(
                option.Value == null ? null : Convert.ToString(option.Value, CultureInfo.InvariantCulture),
                parameterName,
                Option.ToString(option.Type) + " option");
        }

        private static T ConvertIntegralValue<T>(int value, string parameterName, string source)
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(int))
            {
                return (T)(object)value;
            }

            if (targetType == typeof(long))
            {
                return (T)(object)(long)value;
            }

            if (targetType == typeof(string))
            {
                return (T)(object)Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return ConvertStringValue<T>(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                parameterName,
                source);
        }

        private static T ConvertStringValue<T>(string value, string parameterName, string source)
        {
            var targetType = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return default;
                }

                targetType = nullableType;
            }

            if (targetType == typeof(string))
            {
                return (T)(object)value;
            }

            try
            {
                if (targetType == typeof(Guid))
                {
                    return (T)(object)Guid.Parse(value);
                }

                if (targetType == typeof(Uri))
                {
                    return (T)(object)new Uri(value, UriKind.RelativeOrAbsolute);
                }

                if (targetType == typeof(DateTimeOffset))
                {
                    return (T)(object)DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(DateTime))
                {
                    return (T)(object)DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }

                if (targetType == typeof(TimeSpan))
                {
                    return (T)(object)TimeSpan.Parse(value, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(bool))
                {
                    if (string.Equals(value, "1", StringComparison.Ordinal))
                    {
                        return (T)(object)true;
                    }

                    if (string.Equals(value, "0", StringComparison.Ordinal))
                    {
                        return (T)(object)false;
                    }
                }

                if (targetType.IsEnum)
                {
                    return (T)Enum.Parse(targetType, value, ignoreCase: true);
                }

                return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new CoapBadRequestException(
                    "CoAP " + source + " for parameter '" + parameterName + "' cannot be converted to " +
                    targetType.FullName + ".",
                    ex);
            }
        }

        private static T GetMissingValue<T>(
            string parameterName,
            string source,
            bool hasDefaultValue,
            T defaultValue)
        {
            if (hasDefaultValue)
            {
                return defaultValue;
            }

            var targetType = typeof(T);
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
            {
                return default;
            }

            throw new CoapBadRequestException(
                "Required CoAP " + source + " for parameter '" + parameterName + "' is missing.");
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

        private static byte[] CloneBytes(byte[] value)
        {
            return value == null ? null : (byte[])value.Clone();
        }
    }
}
