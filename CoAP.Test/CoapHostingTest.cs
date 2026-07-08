using CoAP.Net;
using CoAP.Server;
using CoAP.Server.Resources;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CoAP
{
    [TestFixture]
    public class CoapHostingTest
    {
        [Test]
        public void AddCoapServer_RegistersHostedServerWithConfiguredEndpoint()
        {
            var services = new ServiceCollection();
            services.AddCoapServer(options => options.ListenLocalhost(0));

            using var provider = services.BuildServiceProvider();
            var server = provider.GetRequiredService<CoapServer>();

            Assert.AreSame(server, provider.GetRequiredService<IServer>());
            Assert.AreEqual(1, server.EndPoints.Count());
            Assert.AreEqual(1, provider.GetServices<IHostedService>().Count());
        }

        [Test]
        public void AddCoapResources_RegistersDataSourceAndMatcher()
        {
            var services = new ServiceCollection();
            services.AddCoapResources(options => options.AddRoute(
                CoapRoute.Get("diagnostics/{target}/status", _ =>
                    new ValueTask<CoapRouteResult>(CoapRouteResult.Changed()))));

            using var provider = services.BuildServiceProvider();
            var dataSource = provider.GetRequiredService<ICoapEndpointDataSource>();
            var matcher = provider.GetRequiredService<ICoapEndpointMatcher>();

            Assert.AreEqual(1, dataSource.Endpoints.Count);
            Assert.IsNotNull(provider.GetRequiredService<CoapRequestDispatcher>());
            Assert.IsNotNull(provider.GetRequiredService<CoapActionInvoker>());
            Assert.IsNotNull(provider.GetRequiredService<ICoapResultExecutor>());
            Assert.IsTrue(matcher.TryMatch(
                new CoapEndpointMatchContext(
                    Method.GET,
                    new[] { "diagnostics", "edge-01", "status" },
                    MediaType.Undefined,
                    MediaType.Undefined,
                    0),
                out var match));
            Assert.AreEqual("edge-01", match.RouteValues["target"]);
        }

        [Test]
        public void MapCoapResources_AddsRegisteredRoutesToServerResourceTree()
        {
            var invoked = false;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddCoapResources(options => options.AddRoute(
                CoapRoute.Get("diagnostics/{target}/status", context =>
                {
                    invoked = true;
                    Assert.AreEqual("edge-01", context.RouteValues["target"]);
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);

            host.MapCoapResources();

            var root = GetRootResource(provider.GetRequiredService<CoapServer>());
            var diagnostics = root.GetChild("diagnostics");
            Assert.IsNotNull(diagnostics);
            var target = diagnostics.GetChild("edge-01");
            Assert.IsNotNull(target);
            var status = target.GetChild("status");
            Assert.IsNotNull(status);
            var exchange = CreateExchange(Method.GET);

            status.HandleRequest(exchange);

            Assert.IsTrue(invoked);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void MapCoapResources_CreatesRequestScopeForInvocation()
        {
            ScopedProbe.DisposedCount = 0;
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddScoped<ScopedProbe>();
            services.AddCoapResources(options => options.AddRoute(
                CoapRoute.Get("diagnostics/{target}/status", context =>
                {
                    Assert.IsNotNull(context.RequestServices);
                    var probe = context.RequestServices.GetRequiredService<ScopedProbe>();
                    probe.WasResolved = true;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                })));

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var status = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("diagnostics")
                .GetChild("edge-01")
                .GetChild("status");

            status.HandleRequest(CreateExchange(Method.GET));

            Assert.AreEqual(1, ScopedProbe.DisposedCount);
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

        private sealed class ScopedProbe : IDisposable
        {
            public static int DisposedCount;

            public bool WasResolved { get; set; }

            public void Dispose()
            {
                if (WasResolved)
                {
                    DisposedCount++;
                }
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
