# IoTSharp.CoAP.NET

IoTSharp.CoAP.NET is a modernized CoAP framework for .NET 10. It keeps the
original CoAP.NET client and server model, blockwise transfer, observe support,
and resource tree, while adding Microsoft.Extensions.Logging integration,
Native AOT analyzer compatibility, and optional DTLS PSK transport.

```powershell
dotnet add package IoTSharp.CoAP.NET --version 3.0.0
```

## Features

- CoAP client APIs for GET, POST, PUT, DELETE, discovery, and observe.
- CoAP server APIs with resource routing and blockwise support.
- UDP transport for `coap://` and DTLS PSK transport for `coaps://`.
- Logging through `Microsoft.Extensions.Logging`.
- Packable as an independent NuGet package.

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

## License

This project preserves the original CoAP.NET BSD-style license. See
[LICENSE](LICENSE) for details.

## Acknowledgements

CoAP.NET is based on Californium, a CoAP framework in Java by Matthias Kovatsch,
Dominique Im Obersteg, and Daniel Pauli at ETH Zurich.
