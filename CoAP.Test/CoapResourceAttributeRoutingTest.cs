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
    public class CoapResourceAttributeRoutingTest
    {
        [Test]
        public void AddCoapResources_DiscoversCoapResourceAttributeActions()
        {
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<InvocationProbe>();
            services.AddCoapResources();

            using var provider = services.BuildServiceProvider();
            var dataSource = provider.GetRequiredService<ICoapEndpointDataSource>();
            var endpoint = dataSource.Endpoints.SingleOrDefault(candidate =>
                candidate.Method == Method.POST &&
                candidate.RoutePattern.Template == "sensors/{sensor}/readings");

            Assert.IsNotNull(endpoint);
            var descriptor = endpoint.Metadata.GetMetadata<CoapResourceActionDescriptor>();
            Assert.IsNotNull(descriptor);
            Assert.AreEqual(typeof(SensorCoapResource), descriptor.Resource.ResourceType);
            Assert.AreEqual(nameof(SensorCoapResource.UploadReadingAsync), descriptor.MethodInfo.Name);
            Assert.AreEqual("Sensor reading upload", endpoint.Metadata.GetMetadata<CoapResourceTitleAttribute>().Title);
        }

        [Test]
        public void MapCoapResources_InvokesCoapResourceActionWithRouteValuesAndServices()
        {
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<InvocationProbe>();
            services.AddCoapResources();

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var readings = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("sensors")
                .GetChild("sensor-01")
                .GetChild("readings");
            var exchange = CreateExchange(Method.POST);
            exchange.Request.Payload = new byte[] { 0x01, 0x02, 0x03 };

            readings.HandleRequest(exchange);

            var probe = provider.GetRequiredService<InvocationProbe>();
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
            Assert.AreEqual("sensor-01", probe.LastSensor);
            Assert.AreEqual(3, probe.LastPayloadLength);
            Assert.IsTrue(probe.ContextWasAvailable);
        }

        [Test]
        public void CoapControllerAttribute_RemainsCompatibilityMarker()
        {
            var services = new ServiceCollection();
            services.AddCoapResources(options => options.AddApplicationPart<CoapResourceAttributeRoutingTest>());

            using var provider = services.BuildServiceProvider();
            var dataSource = provider.GetRequiredService<ICoapEndpointDataSource>();
            var matcher = provider.GetRequiredService<ICoapEndpointMatcher>();

            Assert.IsTrue(matcher.TryMatch(
                new CoapEndpointMatchContext(
                    Method.GET,
                    new[] { "compat", "edge-01", "status" },
                    MediaType.Undefined,
                    MediaType.Undefined,
                    null),
                out var match));
            Assert.AreEqual(typeof(CompatibilityCoapController), match.Endpoint.Metadata.GetMetadata<CoapResourceActionDescriptor>().Resource.ResourceType);
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

        [CoapResource]
        [CoapRoute("sensors/{sensor}")]
        [CoapResourceTitle("Sensor")]
        public sealed class SensorCoapResource : CoapResourceBase
        {
            private readonly InvocationProbe _probe;

            public SensorCoapResource(InvocationProbe probe)
            {
                _probe = probe;
            }

            [CoapPost("readings")]
            [CoapResourceTitle("Sensor reading upload")]
            public ValueTask<CoapRouteResult> UploadReadingAsync(string sensor, CancellationToken cancellationToken)
            {
                _probe.LastSensor = sensor;
                _probe.LastPayloadLength = Payload.Length;
                _probe.ContextWasAvailable = Context.Method == Method.POST && !cancellationToken.IsCancellationRequested;
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }
        }

        [CoapController]
        [CoapRoute("compat/{target}")]
        public sealed class CompatibilityCoapController
        {
            [CoapGet("status")]
            public CoapRouteResult Status(string target)
            {
                return CoapRouteResult.Text(StatusCode.Content, target);
            }
        }

        public sealed class InvocationProbe
        {
            public string LastSensor { get; set; }

            public int LastPayloadLength { get; set; }

            public bool ContextWasAvailable { get; set; }
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
