# CoAP.NET

[![NuGet Version](https://img.shields.io/nuget/v/IoTSharp.CoAP.NET?label=IoTSharp.CoAP.NET)](https://www.nuget.org/packages/IoTSharp.CoAP.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/IoTSharp.CoAP.NET?label=downloads)](https://www.nuget.org/packages/IoTSharp.CoAP.NET)

CoAP.NET is a modernized CoAP framework for .NET 10. It keeps the original
CoAP.NET client and server model, blockwise transfer, observe support, and
resource tree, while adding Microsoft.Extensions.Logging integration, Native
AOT analyzer compatibility, Resource/MVC-style hosting, and optional DTLS PSK
transport.

See [ROADMAP.md](ROADMAP.md) for the CoAP route adapter and low-allocation plan.
The Resource/MVC sample and migration guide are available in
[CoAP.Example/CoAP.ResourceMvc](CoAP.Example/CoAP.ResourceMvc) and
[docs/resource-mvc-migration.md](docs/resource-mvc-migration.md). C12 release
validation is tracked in [docs/c12-release-checklist.md](docs/c12-release-checklist.md).

## NuGet Packages

| Package | Purpose | Install |
| --- | --- | --- |
| [IoTSharp.CoAP.NET](https://www.nuget.org/packages/IoTSharp.CoAP.NET) | Core CoAP client/server, resource tree, blockwise, Observe, DTLS PSK, Generic Host integration, Resource/MVC routing. | `dotnet add package IoTSharp.CoAP.NET --version 3.0.0` |
| [IoTSharp.CoAP.NET.SourceGeneration](https://www.nuget.org/packages/IoTSharp.CoAP.NET.SourceGeneration) | Analyzer package that emits generated Resource/MVC endpoint factories for trim/AOT-friendly hosts. | `dotnet add package IoTSharp.CoAP.NET.SourceGeneration --version 3.0.0` |
| [IoTSharp.CoAP.NET.AspNetCore](https://www.nuget.org/packages/IoTSharp.CoAP.NET.AspNetCore) | ASP.NET Core `IApplicationBuilder` adapter for hosts that want `app.MapCoapResources()`. | `dotnet add package IoTSharp.CoAP.NET.AspNetCore --version 3.0.0` |

## Current Capabilities

- CoAP client APIs for GET, POST, PUT, DELETE, discovery, Observe, synchronous
  calls, and asynchronous calls.
- CoAP server APIs with the original resource tree, `.well-known/core`
  discovery, blockwise transfer, Observe, and resource attributes.
- UDP `coap://` transport and optional PSK-based DTLS `coaps://` transport.
- Host-managed startup through `AddCoapServer()`, `AddCoapResources()`,
  `AddCoapMvc()`, and `MapCoapResources()`.
- Resource/MVC-style programming with `[CoapResource]`, `[CoapRoute]`,
  `[CoapGet]`, `[CoapPost]`, `[CoapPut]`, `[CoapDelete]`, and `[CoapObserve]`.
- Endpoint metadata for method, path, Content-Format, Accept, Observe,
  resource discovery attributes, and authorization policy names.
- Parameter binding for route values, query options, request options, payloads,
  `CancellationToken`, `CoapRouteContext`, and remote endpoint values.
- JSON payload binding through source-generated `System.Text.Json` metadata or
  explicit reflection-based compatibility.
- Endpoint filters, authorization hooks, and request context hooks for
  host-owned identity, tenant, audit, and policy integration.
- Low-allocation route context surfaces, including `ReadOnlyMemory<byte>`
  payload access and compact route value storage.
- `Microsoft.Extensions.Logging` integration and Native AOT analyzer coverage
  for the core package.

## Common Usage

Create a Generic Host server:

```csharp
using CoAP;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCoapServer(options =>
{
    options.ListenAnyIP(5683);
});
builder.Services.AddCoapSystemTextJsonPayloadBinder();
builder.Services.AddCoapResources();

var app = builder.Build();
app.MapCoapResources();
await app.RunAsync();
```

Declare a resource action:

```csharp
using CoAP;
using CoAP.Server.Routing;

[CoapResource]
[CoapRoute("sensors/{sensor}")]
public sealed class SensorCoapResource : CoapResourceBase
{
    [CoapPost("readings")]
    [CoapConsumes(MediaType.ApplicationJson)]
    [CoapProduces(MediaType.ApplicationJson)]
    public CoapRouteResult UploadReading(
        string sensor,
        [CoapFromQuery] int point,
        ReadingPayload payload)
    {
        return CoapRouteResult.Json("{\"ok\":true}");
    }
}

public sealed class ReadingPayload
{
    public double Value { get; set; }
}
```

Use source-generated endpoint factories and JSON metadata in AOT-sensitive
hosts:

```csharp
builder.Services.AddCoapJsonPayloadBinder(MyCoapJsonContext.Default);
builder.Services.AddCoapResources(options =>
{
    options.AddEndpointFactory(MyGeneratedCoapEndpoints.Create);
});
```

Send a client request:

```csharp
using CoAP;

var client = new CoapClient(new Uri("coap://127.0.0.1:5683/sensors/demo/readings?point=1"));
var response = client.Post("{\"value\":42}", MediaType.ApplicationJson);
```

## Resource compatibility baseline

`Resource`, `IResource`, `ResourceAttributes`, `DiscoveryResource`, resource
tree, and `.well-known/core` remain CoAP/CoRE protocol concepts inside
CoAP.NET. They are not host application domain objects. Existing Resource-style
applications can continue to add resources to `CoapServer`, while newer host
integration should use Resource-oriented hosting APIs such as
`AddCoapServer()`, `AddCoapResources()`, and `MapCoapResources()`.

The current compatibility entry points are:

- `AddCoapServer()`, `AddCoapResources()` / `AddCoapMvc()`, and
  `MapCoapResources()` for host-managed server lifetime and resource endpoint
  mapping.
- `CoAP.Server.CoapServer` for server lifetime, endpoint registration, and the
  root resource tree.
- `CoAP.Server.Resources.Resource` / `IResource` for protocol resources,
  method handlers, attributes, observe, and discovery.
- `CoAP.CoapClient` and `CoAP.Request` for synchronous and asynchronous client
  requests, observe, and discovery.
- `CoAP.Channel.DtlsPskChannel` and `DtlsPskClientChannel` for optional DTLS PSK
  transport.
- `CoAP.Server.Routing.CoapRouteEndpoint` for low-level route handler
  compatibility. It is useful for tests and adapters, but it is not the
  recommended future host application API.

## Logging

CoAP.NET no longer ships a custom `CoAP.Log` abstraction. Configure the shared
logger factory once during application startup:

```csharp
using CoAP;

CoapLogging.LoggerFactory = loggerFactory;
```

The default logger factory is `NullLoggerFactory.Instance`, so libraries can use
CoAP.NET without configuring logging.

## Client

```csharp
using CoAP;

var client = new CoapClient(new Uri("coap://127.0.0.1:5683/db/demo/m/cpu?token=secret"))
{
    Timeout = 5000,
};

Response response = client.Post("cpu,host=a value=1 1", MediaType.TextPlain);
```

For DTLS PSK, create an explicit endpoint and assign it to the client:

```csharp
using CoAP;
using CoAP.Channel;
using CoAP.Net;

var config = new CoapConfig();
using var endpoint = new CoAPEndPoint(
    new DtlsPskClientChannel("device-1", "shared-secret"),
    config);
endpoint.Start();

var client = new CoapClient(new Uri("coaps://127.0.0.1:5684/db/demo/m/cpu?token=secret"), config)
{
    EndPoint = endpoint,
    Timeout = 10000,
};

Response response = client.Post("cpu,host=a value=1 1", MediaType.TextPlain);
```

## Server

Recommended host-managed startup:

```csharp
builder.Services.AddCoapServer(options =>
{
    options.ListenAnyIP(5683);
});
builder.Services.AddCoapResources();

var app = builder.Build();
app.MapCoapResources();
app.Run();
```

Native AOT hosts should register generated endpoint factories and
source-generated JSON metadata:

```csharp
builder.Services.AddCoapJsonPayloadBinder(MyCoapJsonContext.Default);
builder.Services.AddCoapResources(options =>
{
    options.AddEndpointFactory(MyGeneratedCoapEndpoints.Create);
});
```

Add the source generator analyzer package to the host project so
`MyGeneratedCoapEndpoints.Create(...)` is emitted during compilation:

```powershell
dotnet add package IoTSharp.CoAP.NET.SourceGeneration --version 3.0.0
```

Reflection-based resource discovery remains available for non-AOT hosts or
compatibility tests:

```csharp
builder.Services.AddCoapResources(options =>
{
    options.AddReflectionResourceDiscovery();
});
```

Resource attribute routing:

```csharp
using CoAP;
using CoAP.Server.Routing;
using System.Threading;
using System.Threading.Tasks;

[CoapResource]
[CoapRoute("sensors/{sensor}")]
public sealed class SensorCoapResource : CoapResourceBase
{
    [CoapPost("readings")]
    [CoapResourceTitle("Sensor reading upload")]
    public ValueTask<CoapRouteResult> UploadReadingAsync(
        string sensor,
        CancellationToken cancellationToken)
    {
        var payload = Payload;
        return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
    }
}
```

Filters and host-owned security hooks:

```csharp
using CoAP;
using CoAP.Server.Routing;

[CoapResource]
[CoapRoute("diagnostics/{target}")]
public sealed class DiagnosticsCoapResource
{
    [CoapGet("status")]
    [CoapAuthorize("diagnostics.read")]
    public CoapRouteResult Status(CoapRouteContext context)
    {
        var tenant = (string)context.Items["tenant"];
        return CoapRouteResult.Text(StatusCode.Content, tenant);
    }
}
```

Hosts register `ICoapRequestContextHook`, `ICoapAuthorizationHook`, and
`ICoapEndpointFilter` implementations in DI; they are resolved from the request
scope when available. CoAP.NET only defines metadata, ordering, and invocation
contracts; tenant isolation, identity checks, audit, and human confirmation
remain host application responsibilities.

Legacy Resource tree compatibility:

```csharp
using CoAP.Server;
using CoAP.Server.Resources;

var server = new CoapServer();
server.Add(new HelloResource());
server.Start();

sealed class HelloResource : Resource
{
    public HelloResource()
        : base("hello")
    {
    }

    protected override void DoGet(CoapExchange exchange)
    {
        exchange.Respond("Hello from CoAP.NET");
    }
}
```

The full Resource-style server sample is in
`CoAP.Example/CoAP.Server`. It registers resources such as `hello`, `storage`,
`large`, `separate`, and `time` by calling `server.Add(new ...Resource(...))`.

The full host-managed Resource/MVC sample is in
`CoAP.Example/CoAP.ResourceMvc`. It demonstrates `AddCoapServer()`,
`AddCoapResources()`, `MapCoapResources()`, JSON DTO binding, binary payloads,
query and option binding, Content-Format / Accept negotiation, Observe
metadata, generated `.well-known/core` output, and stable error responses.

Low-level route endpoint compatibility:

```csharp
using CoAP;
using CoAP.Server;
using CoAP.Server.Routing;
using System.Linq;
using System.Threading.Tasks;

var server = new CoapServer();
var routes = CoapRouteEndpoint.Create(new[]
{
    CoapRoute.Post(
        "diagnostics/{target}/ping",
        () => new DiagnosticsCoapResource(),
        resource => resource.PingAsync())
});

server.Add(routes.ToArray());
server.Start();

sealed class DiagnosticsCoapResource : CoapResourceBase
{
    public ValueTask<CoapRouteResult> PingAsync()
    {
        var target = RouteValues["target"];
        var bytes = Payload;
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
        return new ValueTask<CoapRouteResult>(
            CoapRouteResult.Content(payload, MediaType.ApplicationJson)
                .WithMaxAge(30)
                .WithETag(new byte[] { 0x01, 0x02 })
                .WithLocationPath($"diagnostics/{target}/ping"));
    }
}
```

DTLS PSK server endpoint:

```csharp
using CoAP;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Server;

var config = new CoapConfig();
var server = new CoapServer(config);
var keys = new Dictionary<string, string>
{
    ["device-1"] = "shared-secret",
};

server.AddEndPoint(new CoAPEndPoint(new DtlsPskChannel(5684, keys), config));
server.Start();
```

## Pack

```powershell
dotnet pack CoAP.NET/CoAP.NET.csproj -c Release
dotnet pack CoAP.NET.SourceGeneration/CoAP.NET.SourceGeneration.csproj -c Release
dotnet pack CoAP.NET.AspNetCore/CoAP.NET.AspNetCore.csproj -c Release
```

The core package is written to `CoAP.NET/artifacts` by default. The GitHub
Actions release workflow writes all publishable packages to the workflow
artifact directory before pushing them.

## Publish

NuGet publishing is handled by
[`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml).
The workflow:

- builds the core package, ASP.NET Core adapter, source generator, Resource/MVC
  sample, and tests;
- packs `IoTSharp.CoAP.NET`, `IoTSharp.CoAP.NET.AspNetCore`, and
  `IoTSharp.CoAP.NET.SourceGeneration`;
- publishes to NuGet.org on `v*.*.*` tags by using the organization
  `NUGET_API_KEY` secret;
- supports manual runs with a version input and an explicit `publish_to_nuget`
  switch.

## C0 verification

Run these commands from the `SonnetDB/extensions/IoTSharp.CoAP.NET` directory:

```powershell
dotnet build CoAP.NET/CoAP.NET.csproj
dotnet build CoAP.Example/CoAP.Server/CoAP.Server.csproj
dotnet build CoAP.Example/CoAP.Client/CoAP.Client.csproj
dotnet test CoAP.Test/CoAP.Test.csproj
```

The baseline tests cover:

- Resource tree and discovery: `ResourceTreeTest`, `ResourceAttributesTest`,
  `ResourceTest`.
- Server start/stop and client request flow: `StartStopTest`,
  `CoapClientTest`.
- Blockwise transfer: `BlockwiseTransferTest`, `RandomAccessBlockTest`.
- Observe behavior: `CoapClientTest`, `MemoryLeakingMapTest`.
- Message and option compatibility: `MessageTypeTest`, `OptionTest`,
  `BlockOptionTest`.

DTLS PSK transport is exposed through `DtlsPskChannel` and
`DtlsPskClientChannel`; C12 adds automated route + DTLS PSK smoke coverage in
`CoapC12SmokeTest`.

## C12 verification

Run the C12 smoke tests and route benchmark from this directory:

```powershell
dotnet test CoAP.Test/CoAP.Test.csproj -c Release --filter FullyQualifiedName~CoapC12SmokeTest
dotnet run -c Release --project CoAP.Benchmarks/CoAP.Benchmarks.csproj -- --filter *CoapRouteMatcherBenchmark*
```

The release checklist, trim/AOT warning inventory, and pack verification steps
are maintained in [docs/c12-release-checklist.md](docs/c12-release-checklist.md).

## License

This project preserves the original CoAP.NET BSD-style license. See
[LICENSE](LICENSE) for details.

## Acknowledgements

CoAP.NET is based on Californium, a CoAP framework in Java by Matthias Kovatsch,
Dominique Im Obersteg, and Daniel Pauli at ETH Zurich.
