using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CoAP
{
    [TestFixture]
    public class CoapResourceSourceGenerationTest
    {
        [Test]
        public async Task AddCoapResources_UsesGeneratedEndpointFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton<GeneratedInvocationProbe>();
            services.AddCoapJsonPayloadBinder(GeneratedCoapJsonContext.Default);
            services.AddCoapResources(options => options.AddEndpointFactory(global::MyGeneratedCoapEndpoints.Create));

            using var provider = services.BuildServiceProvider();
            var dataSource = provider.GetRequiredService<ICoapEndpointDataSource>();
            var endpoint = dataSource.Endpoints.Single(candidate =>
                candidate.Method == Method.POST &&
                candidate.RoutePattern.Template == "generated/{device}/readings");

            Assert.AreEqual(
                "Generated reading upload",
                endpoint.Metadata.GetMetadata<CoapResourceTitleAttribute>().Title);
            Assert.IsNotNull(endpoint.Metadata.GetMetadata<CoapConsumesAttribute>());
            Assert.IsNotNull(endpoint.Metadata.GetMetadata<CoapProducesAttribute>());
            Assert.IsNull(endpoint.Metadata.GetMetadata<CoapResourceActionDescriptor>());

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 56830);
            var routeContext = new CoapRouteContext(
                endpoint,
                Method.POST,
                new[] { "generated", "device-01", "readings" },
                new Dictionary<string, string> { ["device"] = "device-01" },
                new[] { "tag=fast", "tag=room%201" },
                Encoding.UTF8.GetBytes("{\"unit\":\"c\",\"value\":21.5}"),
                MediaType.ApplicationJson,
                MediaType.ApplicationJson,
                provider,
                remoteEndPoint: remoteEndPoint);

            var result = await provider
                .GetRequiredService<CoapActionInvoker>()
                .InvokeAsync(new CoapActionInvocationContext(routeContext, provider))
                .ConfigureAwait(false);
            var probe = provider.GetRequiredService<GeneratedInvocationProbe>();

            Assert.AreEqual(StatusCode.Changed, result.StatusCode);
            Assert.AreEqual("device-01", probe.LastDevice);
            Assert.AreEqual(new[] { "fast", "room 1" }, probe.LastTags);
            Assert.AreEqual("c", probe.LastUnit);
            Assert.AreEqual(21.5, probe.LastValue);
            Assert.AreEqual(remoteEndPoint.ToString(), probe.LastRemoteEndPoint);
            Assert.IsTrue(probe.ContextWasAvailable);
        }
    }

    [CoapResource]
    [CoapRoute("generated/{device}")]
    [CoapResourceTitle("Generated sensor")]
    [CoapResourceType("generated.sensor")]
    internal sealed class GeneratedSensorCoapResource : CoapResourceBase
    {
        private readonly GeneratedInvocationProbe _probe;

        public GeneratedSensorCoapResource(GeneratedInvocationProbe probe)
        {
            _probe = probe;
        }

        [CoapPost("readings")]
        [CoapResourceTitle("Generated reading upload")]
        [CoapConsumes(MediaType.ApplicationJson)]
        [CoapProduces(MediaType.ApplicationJson)]
        public ValueTask<CoapRouteResult> UploadAsync(
            string device,
            [CoapFromQuery("tag")] string[] tags,
            GeneratedReadingPayload payload,
            CoapRouteContext context,
            System.Net.EndPoint remoteEndPoint)
        {
            _probe.LastDevice = device;
            _probe.LastTags = tags;
            _probe.LastUnit = payload?.Unit;
            _probe.LastValue = payload?.Value ?? 0;
            _probe.LastRemoteEndPoint = remoteEndPoint?.ToString();
            _probe.ContextWasAvailable = ReferenceEquals(Context, context);
            return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
        }
    }

    internal sealed class GeneratedInvocationProbe
    {
        public string LastDevice { get; set; }

        public string[] LastTags { get; set; }

        public string LastUnit { get; set; }

        public double LastValue { get; set; }

        public string LastRemoteEndPoint { get; set; }

        public bool ContextWasAvailable { get; set; }
    }

    internal sealed class GeneratedReadingPayload
    {
        public string Unit { get; set; }

        public double Value { get; set; }
    }

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(GeneratedReadingPayload))]
    internal sealed partial class GeneratedCoapJsonContext : JsonSerializerContext
    {
    }
}
