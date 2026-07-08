using CoAP;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace CoAP.Examples.ResourceMvc
{
    public static class Program
    {
        private const int Port = 5683;

        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<SensorReadingStore>();
            builder.Services.AddSingleton<ObserveSequence>();
            builder.Services.AddCoapServer(options =>
            {
                options.ListenAnyIP(Port);
            });
            builder.Services.AddCoapJsonPayloadBinder(ResourceMvcJsonContext.Default);
            builder.Services.AddCoapResources(options => options.AddEndpointFactory(global::MyGeneratedCoapEndpoints.Create));

            var app = builder.Build();
            app.MapCoapResources();

            Console.WriteLine("CoAP.NET Resource/MVC sample is listening on coap://localhost:" + Port);
            Console.WriteLine("Discovery:   coap://localhost/.well-known/core");
            Console.WriteLine("JSON POST:   coap://localhost/sensors/demo/readings?point=1");
            Console.WriteLine("Binary POST: coap://localhost/sensors/demo/snapshot");
            Console.WriteLine("Observe:     coap://localhost/sensors/demo/status");
            Console.WriteLine("Fault:       coap://localhost/sensors/demo/fault");

            await app.RunAsync().ConfigureAwait(false);
        }
    }

    [CoapResource]
    [CoapRoute("sensors/{sensor}")]
    [CoapResourceTitle("Sample sensor")]
    [CoapResourceType("sample.sensor")]
    [CoapInterfaceDescription("sensor")]
    public sealed class SensorCoapResource : CoapResourceBase
    {
        private readonly SensorReadingStore _readings;
        private readonly ObserveSequence _observeSequence;

        public SensorCoapResource(SensorReadingStore readings, ObserveSequence observeSequence)
        {
            _readings = readings;
            _observeSequence = observeSequence;
        }

        [CoapGet("latest")]
        [CoapResourceTitle("Latest sensor reading")]
        [CoapProduces(MediaType.ApplicationJson)]
        public CoapRouteResult GetLatest(string sensor, [CoapFromQuery("unit")] string requestedUnit = null)
        {
            var reading = _readings.GetLatest(sensor, requestedUnit);
            return Json(reading, ResourceMvcJsonContext.Default.ReadingState).WithMaxAge(10);
        }

        [CoapPost("readings")]
        [CoapResourceTitle("Upload JSON sensor reading")]
        [CoapConsumes(MediaType.ApplicationJson)]
        [CoapProduces(MediaType.ApplicationJson)]
        public CoapRouteResult UploadReading(
            string sensor,
            [CoapFromQuery] int point,
            [CoapFromQuery("tag")] string[] tags,
            [CoapFromOption(OptionType.ContentFormat)] int contentFormat,
            [CoapFromOption(OptionType.Accept)] int accept,
            ReadingPayload payload,
            CoapRouteContext context,
            System.Net.EndPoint remoteEndPoint)
        {
            var reading = _readings.SaveReading(
                sensor,
                point,
                tags,
                contentFormat,
                accept,
                payload,
                remoteEndPoint);

            return Json(new UploadReadingResponse
            {
                Ok = true,
                Reading = reading,
                Path = string.Join("/", context.PathSegments)
            }, ResourceMvcJsonContext.Default.UploadReadingResponse).WithLocationPath("sensors/" + sensor + "/latest");
        }

        [CoapPost("snapshot")]
        [CoapResourceTitle("Upload binary sensor snapshot")]
        [CoapConsumes(MediaType.ApplicationOctetStream)]
        [CoapProduces(MediaType.ApplicationJson)]
        public CoapRouteResult UploadSnapshot(
            string sensor,
            [CoapFromPayload] ReadOnlyMemory<byte> payload)
        {
            var receipt = _readings.SaveSnapshot(sensor, payload.Length);
            return Json(new UploadSnapshotResponse
            {
                Ok = true,
                Receipt = receipt
            }, ResourceMvcJsonContext.Default.UploadSnapshotResponse).WithLocationPath("sensors/" + sensor + "/snapshot");
        }

        [CoapObserve("status")]
        [CoapResourceTitle("Observable sensor status")]
        [CoapResourceType("sample.sensor.status")]
        [CoapInterfaceDescription("if.s")]
        [CoapProduces(MediaType.ApplicationJson)]
        public CoapRouteResult ObserveStatus(string sensor)
        {
            var observe = _observeSequence.Next();
            return Json(new SensorStatusResponse
            {
                Sensor = sensor,
                Status = "online",
                Observe = observe
            }, ResourceMvcJsonContext.Default.SensorStatusResponse).WithObserve(observe).WithMaxAge(5);
        }

        [CoapGet("fault")]
        [CoapResourceTitle("Sample error response")]
        [CoapProduces(MediaType.TextPlain)]
        public CoapRouteResult Fault(string sensor)
        {
            return CoapRouteResult.Text(
                StatusCode.BadRequest,
                "sample error response for sensor '" + sensor + "'");
        }

        private static CoapRouteResult Json<T>(T value, JsonTypeInfo<T> jsonTypeInfo)
        {
            return CoapRouteResult.Json(JsonSerializer.SerializeToUtf8Bytes(value, jsonTypeInfo));
        }
    }

    public sealed class SensorReadingStore
    {
        private readonly ConcurrentDictionary<string, ReadingState> _readings =
            new ConcurrentDictionary<string, ReadingState>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, SnapshotReceipt> _snapshots =
            new ConcurrentDictionary<string, SnapshotReceipt>(StringComparer.Ordinal);

        public ReadingState GetLatest(string sensor, string requestedUnit)
        {
            if (_readings.TryGetValue(sensor, out var reading))
            {
                if (!string.IsNullOrWhiteSpace(requestedUnit))
                {
                    reading.RequestedUnit = requestedUnit;
                }

                return reading;
            }

            return new ReadingState
            {
                Sensor = sensor,
                Point = 0,
                Unit = requestedUnit ?? "unknown",
                Value = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Source = "default"
            };
        }

        public ReadingState SaveReading(
            string sensor,
            int point,
            string[] tags,
            int contentFormat,
            int accept,
            ReadingPayload payload,
            System.Net.EndPoint remoteEndPoint)
        {
            var reading = new ReadingState
            {
                Sensor = sensor,
                Point = point,
                Tags = tags ?? Array.Empty<string>(),
                Unit = payload?.Unit ?? "unknown",
                Value = payload?.Value ?? 0,
                Timestamp = DateTimeOffset.UtcNow,
                ContentFormat = MediaType.ToString(contentFormat),
                Accept = MediaType.ToString(accept),
                RemoteEndPoint = remoteEndPoint?.ToString(),
                Source = "json"
            };

            _readings[sensor] = reading;
            return reading;
        }

        public SnapshotReceipt SaveSnapshot(string sensor, int byteCount)
        {
            var receipt = new SnapshotReceipt
            {
                Sensor = sensor,
                ByteCount = byteCount,
                Timestamp = DateTimeOffset.UtcNow
            };

            _snapshots[sensor] = receipt;
            return receipt;
        }
    }

    public sealed class ObserveSequence
    {
        private int _value;

        public int Next()
        {
            return Interlocked.Increment(ref _value) & 0x00FFFFFF;
        }
    }

    public sealed class ReadingPayload
    {
        public string Unit { get; set; }

        public double Value { get; set; }
    }

    public sealed class ReadingState
    {
        public string Sensor { get; set; }

        public int Point { get; set; }

        public string[] Tags { get; set; } = Array.Empty<string>();

        public string Unit { get; set; }

        public string RequestedUnit { get; set; }

        public double Value { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string ContentFormat { get; set; }

        public string Accept { get; set; }

        public string RemoteEndPoint { get; set; }

        public string Source { get; set; }
    }

    public sealed class SnapshotReceipt
    {
        public string Sensor { get; set; }

        public int ByteCount { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }

    public sealed class UploadReadingResponse
    {
        public bool Ok { get; set; }

        public ReadingState Reading { get; set; }

        public string Path { get; set; }
    }

    public sealed class UploadSnapshotResponse
    {
        public bool Ok { get; set; }

        public SnapshotReceipt Receipt { get; set; }
    }

    public sealed class SensorStatusResponse
    {
        public string Sensor { get; set; }

        public string Status { get; set; }

        public int Observe { get; set; }
    }

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(ReadingPayload))]
    [JsonSerializable(typeof(ReadingState))]
    [JsonSerializable(typeof(SnapshotReceipt))]
    [JsonSerializable(typeof(UploadReadingResponse))]
    [JsonSerializable(typeof(UploadSnapshotResponse))]
    [JsonSerializable(typeof(SensorStatusResponse))]
    internal sealed partial class ResourceMvcJsonContext : JsonSerializerContext
    {
    }
}
