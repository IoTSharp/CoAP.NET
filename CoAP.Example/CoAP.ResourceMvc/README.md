# CoAP Resource/MVC sample

This sample shows the recommended host-managed CoAP resource model:

```csharp
builder.Services.AddCoapServer(options =>
{
    options.ListenAnyIP(5683);
});
builder.Services.AddCoapJsonPayloadBinder(ResourceMvcJsonContext.Default);
builder.Services.AddCoapResources(options =>
{
    options.AddReflectionResourceDiscovery();
});

var app = builder.Build();
app.MapCoapResources();
app.Run();
```

Run it from the repository root:

```powershell
dotnet run --project CoAP.Example/CoAP.ResourceMvc/CoAP.ResourceMvc.csproj
```

The sample exposes:

- `GET sensors/{sensor}/latest?unit=c`: query binding and JSON response.
- `POST sensors/{sensor}/readings?point=1&tag=fast`: JSON payload binding, Content-Format and Accept binding.
- `POST sensors/{sensor}/snapshot`: binary payload binding.
- `GET/OBSERVE sensors/{sensor}/status`: Observe metadata and JSON response.
- `GET sensors/{sensor}/fault`: explicit error response.
- `.well-known/core`: CoAP.NET-generated discovery output.

Example C# client calls:

```csharp
using CoAP;
using System.Text;

var readingClient = new CoapClient(
    new Uri("coap://localhost/sensors/demo/readings?point=1&tag=fast"));
var reading = readingClient.Post(
    "{\"unit\":\"c\",\"value\":21.5}",
    MediaType.ApplicationJson,
    MediaType.ApplicationJson);

var snapshotClient = new CoapClient(new Uri("coap://localhost/sensors/demo/snapshot"));
var snapshot = snapshotClient.Post(
    Encoding.UTF8.GetBytes("raw-bytes"),
    MediaType.ApplicationOctetStream,
    MediaType.ApplicationJson);

var discoveryClient = new CoapClient(new Uri("coap://localhost/.well-known/core"));
var discovery = discoveryClient.Get(MediaType.ApplicationLinkFormat);
```

Unsupported media negotiation is handled by CoAP.NET before the resource action
is invoked. For example, posting `text/plain` to `sensors/demo/readings` returns
`4.15 Unsupported Content-Format`; requesting `Accept: text/plain` from the same
JSON endpoint returns `4.06 Not Acceptable`.
