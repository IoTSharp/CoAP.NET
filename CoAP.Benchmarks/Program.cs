using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CoAP;
using CoAP.Server.Resources;
using CoAP.Server.Routing;

BenchmarkRunner.Run<CoapRouteMatcherBenchmark>(args: args);

[MemoryDiagnoser]
public class CoapRouteMatcherBenchmark
{
    private CoapEndpointDataSource _dataSource;
    private CoapEndpointMatcher _matcher;
    private CoapEndpointMatchContext _matchContext;

    [GlobalSetup]
    public void Setup()
    {
        var endpoints = new List<CoapEndpoint>(129);
        for (var i = 0; i < 128; i++)
        {
            endpoints.Add(new CoapEndpoint(
                Method.POST,
                "products/p" + i + "/devices/{device}/telemetry",
                HandleAsync,
                new object[]
                {
                    new CoapConsumesAttribute(MediaType.ApplicationJson),
                    new CoapProducesAttribute(MediaType.ApplicationJson),
                },
                "POST product telemetry " + i));
        }

        endpoints.Add(new CoapEndpoint(
            Method.POST,
            "products/target/devices/{device}/telemetry",
            HandleAsync,
            new object[]
            {
                new CoapConsumesAttribute(MediaType.ApplicationJson),
                new CoapProducesAttribute(MediaType.ApplicationJson),
            },
            "POST target telemetry"));

        _dataSource = new CoapEndpointDataSource(endpoints);
        _matcher = new CoapEndpointMatcher(_dataSource);
        _matchContext = new CoapEndpointMatchContext(
            Method.POST,
            new[] { "products", "target", "devices", "edge-01", "telemetry" },
            MediaType.ApplicationJson,
            MediaType.ApplicationJson,
            observe: null);
    }

    [Benchmark]
    public string MatchLastRoute()
    {
        if (!_matcher.TryMatch(_matchContext, out var match))
        {
            throw new InvalidOperationException("Benchmark route did not match.");
        }

        return match.RouteValues["device"];
    }

    [Benchmark]
    public IReadOnlyList<IResource> BuildResourceTree()
    {
        return CoapRouteEndpoint.Create(_dataSource);
    }

    private static ValueTask<CoapRouteResult> HandleAsync(CoapRouteContext context)
    {
        return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
    }
}
