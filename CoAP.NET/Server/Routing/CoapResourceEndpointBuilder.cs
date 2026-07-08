/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using CoAP.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace CoAP.Server.Routing
{
    internal static class CoapResourceEndpointBuilder
    {
        private static readonly Assembly CoapAssembly = typeof(CoapResourceEndpointBuilder).Assembly;
        private static readonly string CoapAssemblyName = CoapAssembly.GetName().Name;

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2072",
            Justification = "Resource types come from application-part reflection; trimmed/AOT hosts must preserve resource members or register endpoints explicitly.")]
        public static IReadOnlyList<CoapEndpoint> Build(CoapMvcOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var endpoints = new List<CoapEndpoint>();
            foreach (var resourceType in DiscoverResourceTypes(options))
            {
                var descriptor = CreateResourceDescriptor(resourceType);
                foreach (var endpoint in CreateEndpoints(descriptor))
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints.Count == 0 ? Array.Empty<CoapEndpoint>() : endpoints.ToArray();
        }

        private static IEnumerable<Type> DiscoverResourceTypes(CoapMvcOptions options)
        {
            var seenTypes = new HashSet<Type>();
            foreach (var assembly in DiscoverApplicationParts(options.ApplicationParts))
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type == null || !seenTypes.Add(type))
                    {
                        continue;
                    }

                    if (IsResourceType(type))
                    {
                        yield return type;
                    }
                }
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026",
            Justification = "CoAP resource discovery scans host application parts at startup; trimmed/AOT hosts must preserve resource assemblies or register endpoints explicitly.")]
        private static IEnumerable<Assembly> DiscoverApplicationParts(IEnumerable<Assembly> configuredParts)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in configuredParts)
            {
                if (assembly != null && AddAssembly(seen, assembly))
                {
                    yield return assembly;
                }
            }

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null && AddAssembly(seen, entryAssembly))
            {
                yield return entryAssembly;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsCandidateApplicationAssembly(assembly) && AddAssembly(seen, assembly))
                {
                    yield return assembly;
                }
            }
        }

        private static bool AddAssembly(HashSet<string> seen, Assembly assembly)
        {
            if (assembly.IsDynamic)
            {
                return false;
            }

            return seen.Add(assembly.FullName ?? assembly.GetName().Name);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026",
            Justification = "Assembly-reference probing is used only for startup discovery; trimmed/AOT hosts can use explicit application parts or endpoint registrations.")]
        private static bool IsCandidateApplicationAssembly(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic || assembly == CoapAssembly)
            {
                return false;
            }

            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name) ||
                name.StartsWith("System.", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                name.StartsWith("NUnit", StringComparison.Ordinal) ||
                string.Equals(name, "System", StringComparison.Ordinal) ||
                string.Equals(name, "Microsoft", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return assembly
                    .GetReferencedAssemblies()
                    .Any(reference => string.Equals(reference.Name, CoapAssemblyName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026",
            Justification = "CoAP resource discovery scans host application parts at startup; trimmed/AOT hosts must preserve resource types or register endpoints explicitly.")]
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        private static bool IsResourceType(Type type)
        {
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
            {
                return false;
            }

            if (type.GetCustomAttribute<CoapResourceAttribute>(inherit: true) != null ||
                type.GetCustomAttribute<CoapControllerAttribute>(inherit: true) != null)
            {
                return true;
            }

            return type.Name.EndsWith("CoapResource", StringComparison.Ordinal) ||
                type.Name.EndsWith("CoapController", StringComparison.Ordinal);
        }

        private static CoapResourceDescriptor CreateResourceDescriptor(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
                DynamicallyAccessedMemberTypes.PublicMethods)]
            Type resourceType)
        {
            var routePrefixes = resourceType
                .GetCustomAttributes<CoapRouteAttribute>(inherit: true)
                .Where(attribute => attribute is not CoapMethodAttribute)
                .ToArray();
            var metadata = resourceType
                .GetCustomAttributes(inherit: true)
                .Where(IsEndpointMetadata)
                .Cast<object>()
                .ToArray();

            return new CoapResourceDescriptor(resourceType, routePrefixes, metadata);
        }

        private static IEnumerable<CoapEndpoint> CreateEndpoints(CoapResourceDescriptor resource)
        {
            var methods = resource.ResourceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            foreach (var method in methods)
            {
                if (method.IsSpecialName || method.ContainsGenericParameters)
                {
                    continue;
                }

                var methodRoutes = method.GetCustomAttributes<CoapMethodAttribute>(inherit: true).ToArray();
                if (methodRoutes.Length == 0)
                {
                    continue;
                }

                var routePrefixes = resource.RoutePrefixes.Count == 0
                    ? new CoapRouteAttribute[] { new CoapRouteAttribute(string.Empty) }
                    : resource.RoutePrefixes;

                foreach (var prefix in routePrefixes)
                {
                    foreach (var methodRoute in methodRoutes)
                    {
                        var routeTemplate = CombineTemplates(prefix.Template, methodRoute.Template);
                        var metadata = CreateMetadata(resource, method, methodRoute);
                        var descriptor = new CoapResourceActionDescriptor(
                            resource,
                            method,
                            methodRoute.Method,
                            routeTemplate,
                            methodRoute,
                            method.GetParameters(),
                            method.ReturnType,
                            metadata);
                        var endpointMetadata = new List<object>(metadata.Count + 1);
                        endpointMetadata.AddRange(metadata);
                        endpointMetadata.Add(descriptor);

                        yield return new CoapEndpoint(
                            descriptor.CoapMethod,
                            descriptor.RouteTemplate,
                            context => InvokeActionAsync(descriptor, context),
                            endpointMetadata,
                            descriptor.DisplayName);
                    }
                }
            }
        }

        private static IReadOnlyList<object> CreateMetadata(
            CoapResourceDescriptor resource,
            MethodInfo method,
            CoapMethodAttribute methodRoute)
        {
            var metadata = new List<object>();
            metadata.AddRange(resource.Metadata);
            metadata.AddRange(method.GetCustomAttributes(inherit: true).Where(IsEndpointMetadata).Cast<object>());
            metadata.Add(methodRoute);
            return metadata;
        }

        private static bool IsEndpointMetadata(object value)
        {
            return value is not CoapRouteAttribute;
        }

        private static string CombineTemplates(string prefix, string template)
        {
            var normalizedPrefix = (prefix ?? string.Empty).Trim('/');
            var normalizedTemplate = (template ?? string.Empty).Trim('/');
            if (normalizedPrefix.Length == 0)
            {
                return normalizedTemplate;
            }

            if (normalizedTemplate.Length == 0)
            {
                return normalizedPrefix;
            }

            return normalizedPrefix + "/" + normalizedTemplate;
        }

        private static async ValueTask<CoapRouteResult> InvokeActionAsync(
            CoapResourceActionDescriptor descriptor,
            CoapRouteContext context)
        {
            var resource = CreateResourceInstance(descriptor.Resource.ResourceType, context.RequestServices);
            if (resource is CoapResourceBase resourceBase)
            {
                return await resourceBase
                    .InvokeWithContextAsync(context, () => InvokeActionCoreAsync(resource, descriptor, context))
                    .ConfigureAwait(false);
            }

            return await InvokeActionCoreAsync(resource, descriptor, context).ConfigureAwait(false);
        }

        private static object CreateResourceInstance(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            Type resourceType,
            IServiceProvider requestServices)
        {
            if (requestServices != null)
            {
                return ActivatorUtilities.CreateInstance(requestServices, resourceType);
            }

            try
            {
                return Activator.CreateInstance(resourceType);
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    "CoAP resource type '" + resourceType.FullName + "' requires request services but no service provider is available.",
                    ex);
            }
        }

        private static async ValueTask<CoapRouteResult> InvokeActionCoreAsync(
            object resource,
            CoapResourceActionDescriptor descriptor,
            CoapRouteContext context)
        {
            var arguments = CoapActionParameterBinder.BindArguments(descriptor, context);
            object rawResult;
            try
            {
                rawResult = descriptor.MethodInfo.Invoke(resource, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            return await ConvertResultAsync(rawResult).ConfigureAwait(false);
        }

        private static async ValueTask<CoapRouteResult> ConvertResultAsync(object result)
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
                default:
                    throw new InvalidOperationException(
                        "Unsupported CoAP resource action return type '" + result.GetType().FullName + "'.");
            }
        }
    }
}
