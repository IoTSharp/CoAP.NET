# CoAP.NET

CoAP.NET is a modernized CoAP framework for .NET 10. It keeps the original
CoAP.NET client and server model, blockwise transfer, observe support, and
resource tree, while adding Microsoft.Extensions.Logging integration, Native
AOT analyzer compatibility, and optional DTLS PSK transport.

See [ROADMAP.md](ROADMAP.md) for the CoAP route adapter and low-allocation plan.

```powershell
dotnet add package <coap-package-id> --version 3.0.0
```

## Features

- CoAP client APIs for GET, POST, PUT, DELETE, discovery, and observe.
- CoAP server APIs with resource routing and blockwise support.
- UDP transport for `coap://` and DTLS PSK transport for `coaps://`.
- Host-integrated startup through `AddCoapServer()`, `AddCoapResources()`,
  `AddCoapMvc()`, and `MapCoapResources()`.
- Logging through `Microsoft.Extensions.Logging`.
- Packable as an independent NuGet package.

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
```

The package is written to `CoAP.NET/artifacts` by default.

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
`DtlsPskClientChannel`; the README examples above are the current smoke entry
until the later performance and transport smoke phase expands automated DTLS
coverage.

## License

This project preserves the original CoAP.NET BSD-style license. See
[LICENSE](LICENSE) for details.

## Acknowledgements

CoAP.NET is based on Californium, a CoAP framework in Java by Matthias Kovatsch,
Dominique Im Obersteg, and Daniel Pauli at ETH Zurich.
