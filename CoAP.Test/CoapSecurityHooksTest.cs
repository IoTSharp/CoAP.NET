using CoAP.Net;
using CoAP.Server;
using CoAP.Server.Resources;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CoAP
{
    [TestFixture]
    public class CoapSecurityHooksTest
    {
        [Test]
        public void EndpointFilter_CanShortCircuitActionAndWrapResult()
        {
            var invoked = false;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<HookProbe>();
            services.AddSingleton<ICoapEndpointFilter, ProbeEndpointFilter>();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.GET,
                "c10/filtered",
                _ =>
                {
                    invoked = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { new ShortCircuitFilterAttribute() })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var filtered = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("filtered");
            var exchange = CreateExchange(Method.GET);

            filtered.HandleRequest(exchange);

            var probe = provider.GetRequiredService<HookProbe>();
            Assert.IsFalse(invoked);
            Assert.AreEqual(StatusCode.Forbidden, exchange.SentResponse.StatusCode);
            Assert.AreEqual("filtered", exchange.SentResponse.PayloadString);
            CollectionAssert.AreEqual(
                new[] { "global-before", "metadata-short", "global-after:Forbidden" },
                probe.Events);
        }

        [Test]
        public void AuthorizationHook_CanRejectEndpointBeforeAction()
        {
            var invoked = false;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<HookProbe>();
            services.AddSingleton<ICoapAuthorizationHook, DenyAuthorizationHook>();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.POST,
                "c10/secure",
                _ =>
                {
                    invoked = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { new CoapAuthorizeAttribute("edge.write") })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var secure = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("secure");
            var exchange = CreateExchange(Method.POST);

            secure.HandleRequest(exchange);

            var probe = provider.GetRequiredService<HookProbe>();
            Assert.IsFalse(invoked);
            Assert.AreEqual(StatusCode.Unauthorized, exchange.SentResponse.StatusCode);
            Assert.AreEqual("denied", exchange.SentResponse.PayloadString);
            CollectionAssert.AreEqual(new[] { "auth:edge.write" }, probe.Events);
        }

        [Test]
        public void ContextHook_PopulatesItemsBeforeAuthorizationAndAction()
        {
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<HookProbe>();
            services.AddSingleton<ICoapRequestContextHook, TenantContextHook>();
            services.AddSingleton<ICoapAuthorizationHook, TenantAuthorizationHook>();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.GET,
                "c10/context/{device}",
                context =>
                {
                    var tenant = (string)context.Items["tenant"];
                    return new ValueTask<CoapRouteResult>(
                        CoapRouteResult.Text(StatusCode.Content, tenant + ":" + context.RouteValues["device"]));
                },
                new object[] { new CoapAuthorizeAttribute("tenant.read") })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var contextResource = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("context")
                .GetChild("device-01");
            var exchange = CreateExchange(Method.GET);

            contextResource.HandleRequest(exchange);

            var probe = provider.GetRequiredService<HookProbe>();
            Assert.AreEqual(StatusCode.Content, exchange.SentResponse.StatusCode);
            Assert.AreEqual("tenant-a:device-01", exchange.SentResponse.PayloadString);
            CollectionAssert.AreEqual(
                new[] { "context", "auth:tenant.read:tenant-a" },
                probe.Events);
        }

        [Test]
        public void AuthorizationMetadata_DeniesByDefaultWhenHookIsMissing()
        {
            var invoked = false;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.GET,
                "c10/misconfigured",
                _ =>
                {
                    invoked = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { new CoapAuthorizeAttribute() })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var secure = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("misconfigured");
            var exchange = CreateExchange(Method.GET);

            secure.HandleRequest(exchange);

            Assert.IsFalse(invoked);
            Assert.AreEqual(StatusCode.Forbidden, exchange.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route authorization hook is not configured.", exchange.SentResponse.PayloadString);
        }

        [Test]
        public void AllowAnonymousMetadata_SkipsAuthorizationHook()
        {
            var invoked = false;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<HookProbe>();
            services.AddSingleton<ICoapAuthorizationHook, DenyAuthorizationHook>();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.GET,
                "c10/public",
                _ =>
                {
                    invoked = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { new CoapAuthorizeAttribute("edge.write"), new CoapAllowAnonymousAttribute() })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var publicEndpoint = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("public");
            var exchange = CreateExchange(Method.GET);

            publicEndpoint.HandleRequest(exchange);

            var probe = provider.GetRequiredService<HookProbe>();
            Assert.IsTrue(invoked);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
            CollectionAssert.IsEmpty(probe.Events);
        }

        [Test]
        public void HooksAndFilters_AreResolvedFromRequestScope()
        {
            ScopedC10Probe.DisposedCount = 0;
            ScopedC10Probe.SameScopeHits = 0;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddScoped<ScopedC10Probe>();
            services.AddScoped<ICoapRequestContextHook, ScopedContextHook>();
            services.AddScoped<ICoapAuthorizationHook, ScopedAuthorizationHook>();
            services.AddScoped<ICoapEndpointFilter, ScopedEndpointFilter>();
            services.AddCoapResources(options => options.AddEndpoint(new CoapEndpoint(
                Method.GET,
                "c10/scoped",
                context =>
                {
                    var probe = context.RequestServices.GetRequiredService<ScopedC10Probe>();
                    if (ReferenceEquals(probe, context.Items["probe"]))
                    {
                        ScopedC10Probe.SameScopeHits++;
                    }

                    probe.ActionSeen = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { new CoapAuthorizeAttribute("scoped") })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var scoped = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("c10")
                .GetChild("scoped");

            scoped.HandleRequest(CreateExchange(Method.GET));

            Assert.AreEqual(3, ScopedC10Probe.SameScopeHits);
            Assert.AreEqual(1, ScopedC10Probe.DisposedCount);
        }

        private static IResource GetRootResource(CoapServer server)
        {
            var field = typeof(CoapServer).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (IResource)field.GetValue(server);
        }

        private static CapturingExchange CreateExchange(Method method)
        {
            var request = new Request(method)
            {
                Source = new IPEndPoint(IPAddress.Loopback, 56830)
            };
            return new CapturingExchange(request);
        }

        private sealed class HookProbe
        {
            public IList<string> Events { get; } = new List<string>();
        }

        private sealed class ProbeEndpointFilter : ICoapEndpointFilter
        {
            private readonly HookProbe _probe;

            public ProbeEndpointFilter(HookProbe probe)
            {
                _probe = probe;
            }

            public async ValueTask<ICoapResult> InvokeAsync(
                CoapActionInvocationContext context,
                CoapEndpointFilterDelegate next)
            {
                _probe.Events.Add("global-before");
                var result = await next(context).ConfigureAwait(false);
                _probe.Events.Add("global-after:" + result.StatusCode);
                return result;
            }
        }

        private sealed class ShortCircuitFilterAttribute : CoapEndpointFilterAttribute
        {
            public override ValueTask<ICoapResult> InvokeAsync(
                CoapActionInvocationContext context,
                CoapEndpointFilterDelegate next)
            {
                var probe = context.RequestServices.GetRequiredService<HookProbe>();
                probe.Events.Add("metadata-short");
                return new ValueTask<ICoapResult>(
                    CoapRouteResult.Text(StatusCode.Forbidden, "filtered"));
            }
        }

        private sealed class DenyAuthorizationHook : ICoapAuthorizationHook
        {
            private readonly HookProbe _probe;

            public DenyAuthorizationHook(HookProbe probe)
            {
                _probe = probe;
            }

            public ValueTask<CoapAuthorizationResult> AuthorizeAsync(CoapAuthorizationContext context)
            {
                _probe.Events.Add("auth:" + context.Requirements.Single().Policy);
                return new ValueTask<CoapAuthorizationResult>(
                    CoapAuthorizationResult.Fail(StatusCode.Unauthorized, "denied"));
            }
        }

        private sealed class TenantContextHook : ICoapRequestContextHook
        {
            private readonly HookProbe _probe;

            public TenantContextHook(HookProbe probe)
            {
                _probe = probe;
            }

            public ValueTask EnrichAsync(CoapRequestContextHookContext context)
            {
                context.Items["tenant"] = "tenant-a";
                _probe.Events.Add("context");
                return default;
            }
        }

        private sealed class TenantAuthorizationHook : ICoapAuthorizationHook
        {
            private readonly HookProbe _probe;

            public TenantAuthorizationHook(HookProbe probe)
            {
                _probe = probe;
            }

            public ValueTask<CoapAuthorizationResult> AuthorizeAsync(CoapAuthorizationContext context)
            {
                var tenant = (string)context.Items["tenant"];
                _probe.Events.Add("auth:" + context.Requirements.Single().Policy + ":" + tenant);
                return new ValueTask<CoapAuthorizationResult>(CoapAuthorizationResult.Success());
            }
        }

        private sealed class ScopedC10Probe : IDisposable
        {
            public static int DisposedCount;

            public static int SameScopeHits;

            public bool ActionSeen { get; set; }

            public void Dispose()
            {
                if (ActionSeen)
                {
                    DisposedCount++;
                }
            }
        }

        private sealed class ScopedContextHook : ICoapRequestContextHook
        {
            private readonly ScopedC10Probe _probe;

            public ScopedContextHook(ScopedC10Probe probe)
            {
                _probe = probe;
            }

            public ValueTask EnrichAsync(CoapRequestContextHookContext context)
            {
                context.Items["probe"] = _probe;
                return default;
            }
        }

        private sealed class ScopedAuthorizationHook : ICoapAuthorizationHook
        {
            private readonly ScopedC10Probe _probe;

            public ScopedAuthorizationHook(ScopedC10Probe probe)
            {
                _probe = probe;
            }

            public ValueTask<CoapAuthorizationResult> AuthorizeAsync(CoapAuthorizationContext context)
            {
                if (ReferenceEquals(_probe, context.Items["probe"]))
                {
                    ScopedC10Probe.SameScopeHits++;
                }

                return new ValueTask<CoapAuthorizationResult>(CoapAuthorizationResult.Success());
            }
        }

        private sealed class ScopedEndpointFilter : ICoapEndpointFilter
        {
            private readonly ScopedC10Probe _probe;

            public ScopedEndpointFilter(ScopedC10Probe probe)
            {
                _probe = probe;
            }

            public ValueTask<ICoapResult> InvokeAsync(
                CoapActionInvocationContext context,
                CoapEndpointFilterDelegate next)
            {
                if (ReferenceEquals(_probe, context.Items["probe"]))
                {
                    ScopedC10Probe.SameScopeHits++;
                }

                return next(context);
            }
        }

        private sealed class CapturingExchange : Exchange
        {
            public CapturingExchange(Request request)
                : base(request, Origin.Remote)
            {
                Request = request ?? throw new ArgumentNullException(nameof(request));
            }

            public Response SentResponse { get; private set; }

            public override void SendResponse(Response response)
            {
                SentResponse = response;
                Response = response;
            }
        }

        private sealed class TestHost : IHost
        {
            public TestHost(IServiceProvider services)
            {
                Services = services ?? throw new ArgumentNullException(nameof(services));
            }

            public IServiceProvider Services { get; }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
