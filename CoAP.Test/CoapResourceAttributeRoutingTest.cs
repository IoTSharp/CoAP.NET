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
using System.Text;
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

        [Test]
        public void MapCoapResources_BindsC8ActionParameters()
        {
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<InvocationProbe>();
            services.AddCoapResources();

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var deviceId = Guid.NewGuid();
            var samples = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("bindings")
                .GetChild(deviceId.ToString())
                .GetChild("samples");
            var exchange = CreateExchange(Method.POST);
            exchange.Request.Payload = Encoding.UTF8.GetBytes("{\"unit\":\"c\",\"value\":21.5}");
            exchange.Request.ContentFormat = MediaType.ApplicationJson;
            exchange.Request.Accept = MediaType.ApplicationJson;
            exchange.Request.Observe = 0;
            exchange.Request.SetBlock1(BlockOption.EncodeSZX(64), false, 3);
            exchange.Request.AddETag(new byte[] { 0x0A, 0x0B });
            exchange.Request.AddUriQuery("point=42");
            exchange.Request.AddUriQuery("tag=fast");
            exchange.Request.AddUriQuery("tag=room%201");

            samples.HandleRequest(exchange);

            var probe = provider.GetRequiredService<InvocationProbe>();
            Assert.AreEqual(StatusCode.Content, exchange.SentResponse.StatusCode, exchange.SentResponse.PayloadString);
            Assert.AreEqual(MediaType.ApplicationJson, exchange.SentResponse.ContentFormat);
            Assert.AreEqual(deviceId, probe.LastDeviceId);
            Assert.AreEqual(42, probe.LastPoint);
            CollectionAssert.AreEqual(new[] { "fast", "room 1" }, probe.LastTags);
            Assert.AreEqual(MediaType.ApplicationJson, probe.LastContentFormat);
            Assert.AreEqual(MediaType.ApplicationJson, probe.LastAccept);
            Assert.AreEqual(0, probe.LastObserve);
            Assert.AreEqual(3, probe.LastBlockNumber);
            Assert.AreEqual(64, probe.LastBlockSize);
            Assert.AreEqual("c", probe.LastPayloadUnit);
            Assert.AreEqual(21.5, probe.LastPayloadValue);
            Assert.AreEqual(IPAddress.Loopback, ((IPEndPoint)probe.LastRemoteEndPoint).Address);
            CollectionAssert.AreEqual(new byte[] { 0x0A, 0x0B }, probe.LastETags.Single());
        }

        [Test]
        public void MapCoapResources_RejectsUnsupportedContentFormatAndAccept()
        {
            var services = new ServiceCollection();
            services.AddCoapServer();
            services.AddSingleton<InvocationProbe>();
            services.AddCoapResources();

            using var provider = services.BuildServiceProvider();
            var host = new TestHost(provider);
            host.MapCoapResources();
            var deviceId = Guid.NewGuid();
            var samples = GetRootResource(provider.GetRequiredService<CoapServer>())
                .GetChild("bindings")
                .GetChild(deviceId.ToString())
                .GetChild("samples");

            var unsupportedContent = CreateExchange(Method.POST);
            unsupportedContent.Request.Payload = Encoding.UTF8.GetBytes("plain");
            unsupportedContent.Request.ContentFormat = MediaType.TextPlain;
            unsupportedContent.Request.Accept = MediaType.ApplicationJson;
            unsupportedContent.Request.AddUriQuery("point=42");

            samples.HandleRequest(unsupportedContent);

            Assert.AreEqual(StatusCode.UnsupportedMediaType, unsupportedContent.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route media type is not supported.", unsupportedContent.SentResponse.PayloadString);

            var unacceptable = CreateExchange(Method.POST);
            unacceptable.Request.Payload = Encoding.UTF8.GetBytes("{\"unit\":\"c\",\"value\":21.5}");
            unacceptable.Request.ContentFormat = MediaType.ApplicationJson;
            unacceptable.Request.Accept = MediaType.TextPlain;
            unacceptable.Request.AddUriQuery("point=42");

            samples.HandleRequest(unacceptable);

            Assert.AreEqual(StatusCode.NotAcceptable, unacceptable.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route response media type is not acceptable.", unacceptable.SentResponse.PayloadString);
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

        [CoapResource]
        [CoapRoute("bindings/{deviceId}")]
        public sealed class BindingCoapResource
        {
            private readonly InvocationProbe _probe;

            public BindingCoapResource(InvocationProbe probe)
            {
                _probe = probe;
            }

            [CoapPost("samples")]
            [CoapConsumes(MediaType.ApplicationJson)]
            [CoapProduces(MediaType.ApplicationJson)]
            public CoapRouteResult Upload(
                Guid deviceId,
                [CoapFromQuery] int point,
                [CoapFromQuery("tag")] string[] tags,
                [CoapFromOption(OptionType.ContentFormat)] int contentFormat,
                [CoapFromOption(OptionType.Accept)] int accept,
                [CoapFromOption(OptionType.Observe)] int? observe,
                [CoapFromOption(OptionType.Block1)] BlockOption block1,
                [CoapFromOption(OptionType.ETag)] IReadOnlyList<byte[]> eTags,
                ReadingPayload payload,
                CoapRouteContext context,
                System.Net.EndPoint remoteEndPoint)
            {
                _probe.LastDeviceId = deviceId;
                _probe.LastPoint = point;
                _probe.LastTags = tags;
                _probe.LastContentFormat = contentFormat;
                _probe.LastAccept = accept;
                _probe.LastObserve = observe;
                _probe.LastBlockNumber = block1.NUM;
                _probe.LastBlockSize = block1.Size;
                _probe.LastETags = eTags;
                _probe.LastPayloadUnit = payload.Unit;
                _probe.LastPayloadValue = payload.Value;
                _probe.LastRemoteEndPoint = remoteEndPoint;
                _probe.ContextWasAvailable = context.Method == Method.POST;
                return CoapRouteResult.Json("{\"ok\":true}");
            }
        }

        public sealed class ReadingPayload
        {
            public string Unit { get; set; }

            public double Value { get; set; }
        }

        public sealed class InvocationProbe
        {
            public string LastSensor { get; set; }

            public int LastPayloadLength { get; set; }

            public bool ContextWasAvailable { get; set; }

            public Guid LastDeviceId { get; set; }

            public int LastPoint { get; set; }

            public string[] LastTags { get; set; }

            public int LastContentFormat { get; set; }

            public int LastAccept { get; set; }

            public int? LastObserve { get; set; }

            public int LastBlockNumber { get; set; }

            public int LastBlockSize { get; set; }

            public IReadOnlyList<byte[]> LastETags { get; set; }

            public string LastPayloadUnit { get; set; }

            public double LastPayloadValue { get; set; }

            public System.Net.EndPoint LastRemoteEndPoint { get; set; }
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
