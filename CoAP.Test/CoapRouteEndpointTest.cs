using CoAP.Net;
using CoAP.Observe;
using CoAP.Server.Resources;
using CoAP.Server.Routing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoAP
{
    [TestFixture]
    public class CoapRouteEndpointTest
    {
        [Test]
        public void HandleRequest_WaitsForAsyncHandler_AndSendsChanged()
        {
            var handlerCompleted = false;
            var endpoint = CreateEndpoint(CoapRoute.Post("diagnostics/{target}/ping", async context =>
            {
                await Task.Yield();
                Assert.AreEqual("edge-01", context.RouteValues["target"]);
                handlerCompleted = true;
                return CoapRouteResult.Changed();
            }), "edge-01", "ping");
            var exchange = CreateExchange(Method.POST);

            endpoint.HandleRequest(exchange);

            Assert.IsTrue(handlerCompleted);
            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void HandleRequest_WritesPayloadAndResponseOptions()
        {
            var eTag = new byte[] { 0x01, 0x02, 0x03 };
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/status", context =>
            {
                Assert.AreEqual("edge-01", context.RouteValues["target"]);
                return new ValueTask<CoapRouteResult>(
                    CoapRouteResult.Json("{\"ok\":true}")
                        .WithETag(eTag)
                        .WithMaxAge(30)
                        .WithLocationPath("diagnostics/edge-01/status")
                        .WithLocationQuery("v=1"));
            }), "edge-01", "status");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.Content, exchange.SentResponse.StatusCode);
            Assert.AreEqual(MediaType.ApplicationJson, exchange.SentResponse.ContentFormat);
            Assert.AreEqual("{\"ok\":true}", exchange.SentResponse.PayloadString);
            Assert.AreEqual(30, exchange.SentResponse.MaxAge);
            Assert.AreEqual("diagnostics/edge-01/status", exchange.SentResponse.LocationPath);
            Assert.AreEqual("v=1", exchange.SentResponse.LocationQuery);
            CollectionAssert.AreEqual(eTag, exchange.SentResponse.ETags.Single());
        }

        [Test]
        public void HandleRequest_ProvidesPayloadAndRequestOptions()
        {
            var payload = Encoding.UTF8.GetBytes("{\"temperature\":21}");
            var endpoint = CreateEndpoint(CoapRoute.Post("diagnostics/{target}/samples", context =>
            {
                Assert.AreEqual("edge-01", context.RouteValues["target"]);
                Assert.AreEqual(MediaType.ApplicationJson, context.ContentFormat);
                Assert.AreEqual(MediaType.TextPlain, context.Accept);
                CollectionAssert.AreEqual(payload, context.Payload.ToArray());
                CollectionAssert.AreEqual(new[] { "mode=raw", "unit=c" }, context.Queries);
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }), "edge-01", "samples");
            var exchange = CreateExchange(Method.POST);
            exchange.Request.Payload = payload;
            exchange.Request.ContentFormat = MediaType.ApplicationJson;
            exchange.Request.Accept = MediaType.TextPlain;
            exchange.Request.AddUriQuery("mode=raw");
            exchange.Request.AddUriQuery("unit=c");

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void HandleRequest_RouteValuesUseReadOnlyDictionarySemantics()
        {
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/points/{point}", context =>
            {
                Assert.AreEqual(2, context.RouteValues.Count);
                Assert.IsTrue(context.RouteValues.ContainsKey("target"));
                Assert.IsTrue(context.RouteValues.TryGetValue("point", out var point));
                Assert.AreEqual("temp", point);
                Assert.AreEqual("edge-01", context.RouteValues["target"]);
                CollectionAssert.AreEqual(new[] { "target", "point" }, context.RouteValues.Keys.ToArray());
                CollectionAssert.AreEqual(new[] { "edge-01", "temp" }, context.RouteValues.Values.ToArray());
                CollectionAssert.AreEqual(
                    new[]
                    {
                        new KeyValuePair<string, string>("target", "edge-01"),
                        new KeyValuePair<string, string>("point", "temp")
                    },
                    context.RouteValues.ToArray());
                Assert.Throws<KeyNotFoundException>(() =>
                {
                    var ignored = context.RouteValues["missing"];
                });
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }), "edge-01", "points", "temp");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void HandleRequest_DuplicateRouteParameterUsesLastValue()
        {
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/mirror/{target}", context =>
            {
                Assert.AreEqual(1, context.RouteValues.Count);
                Assert.AreEqual("edge-right", context.RouteValues["target"]);
                CollectionAssert.AreEqual(new[] { "target" }, context.RouteValues.Keys.ToArray());
                CollectionAssert.AreEqual(new[] { "edge-right" }, context.RouteValues.Values.ToArray());
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }), "edge-left", "mirror", "edge-right");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void HandleRequest_LiteralRouteUsesSharedEmptyRouteValues()
        {
            IReadOnlyDictionary<string, string> firstRouteValues = null;
            IReadOnlyDictionary<string, string> secondRouteValues = null;
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/ping", context =>
            {
                if (firstRouteValues == null)
                {
                    firstRouteValues = context.RouteValues;
                }
                else
                {
                    secondRouteValues = context.RouteValues;
                }

                Assert.AreEqual(0, context.RouteValues.Count);
                Assert.IsFalse(context.RouteValues.ContainsKey("target"));
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }), "ping");

            endpoint.HandleRequest(CreateExchange(Method.GET));
            endpoint.HandleRequest(CreateExchange(Method.GET));

            Assert.IsNotNull(firstRouteValues);
            Assert.IsNotNull(secondRouteValues);
            Assert.AreSame(firstRouteValues, secondRouteValues);
        }

        [Test]
        public void RoutePattern_ParsesSegmentsParametersAndConstraints()
        {
            var pattern = CoapRoutePattern.Parse("/diagnostics/{target}/{point:int}");

            Assert.AreEqual("diagnostics/{target}/{point:int}", pattern.Template);
            Assert.AreEqual("diagnostics", pattern.RootSegment);
            Assert.AreEqual(3, pattern.Segments.Count);
            Assert.IsFalse(pattern.Segments[0].IsParameter);
            Assert.AreEqual("diagnostics", pattern.Segments[0].Literal);
            Assert.IsTrue(pattern.Segments[1].IsParameter);
            Assert.AreEqual("target", pattern.Segments[1].ParameterName);
            Assert.IsTrue(pattern.Segments[2].IsParameter);
            Assert.AreEqual("point", pattern.Segments[2].ParameterName);
            Assert.AreEqual("int", pattern.Segments[2].Constraint);
            CollectionAssert.AreEqual(new[] { "target", "point" }, pattern.ParameterNames);
            Assert.IsTrue(pattern.IsPrefix(new[] { "diagnostics", "edge-01" }));
            Assert.IsTrue(pattern.TryMatch(
                new[] { "diagnostics", "edge-01", "42" },
                out var routeValues));
            Assert.AreEqual("edge-01", routeValues["target"]);
            Assert.AreEqual("42", routeValues["point"]);
        }

        [Test]
        public void EndpointMetadataCollection_ReturnsMostSpecificMetadata()
        {
            var metadata = new CoapEndpointMetadataCollection("first", 42, "last");

            Assert.AreEqual(3, metadata.Count);
            Assert.AreEqual("last", metadata.GetMetadata<string>());
            CollectionAssert.AreEqual(new[] { "first", "last" }, metadata.OfType<string>().ToArray());
        }

        [Test]
        public void EndpointMatcher_MatchesEndpointFromDataSource()
        {
            var endpoint = new CoapEndpoint(
                Method.POST,
                "diagnostics/{target}/ping",
                _ => new ValueTask<CoapRouteResult>(CoapRouteResult.Changed()));
            var dataSource = new CoapEndpointDataSource(new[] { endpoint });
            var matcher = new CoapEndpointMatcher(dataSource);

            var matched = matcher.TryMatch(
                new CoapEndpointMatchContext(
                    Method.POST,
                    new[] { "diagnostics", "edge-01", "ping" },
                    MediaType.ApplicationJson,
                    MediaType.TextPlain,
                    0),
                out var match);

            Assert.IsTrue(matched);
            Assert.AreSame(endpoint, match.Endpoint);
            Assert.AreEqual("edge-01", match.RouteValues["target"]);
        }

        [Test]
        public void HandleRequest_CanInvokeEndpointDataSourceWithoutLegacyRoute()
        {
            CoapRouteContext capturedContext = null;
            var endpointDescriptor = new CoapEndpoint(
                Method.GET,
                "diagnostics/{target}/status",
                context =>
                {
                    capturedContext = context;
                    return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
                },
                new object[] { "diagnostic-metadata" },
                "diagnostics status");
            var endpoint = CreateEndpoint(
                new CoapEndpointDataSource(new[] { endpointDescriptor }),
                "edge-01",
                "status");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(capturedContext);
            Assert.AreSame(endpointDescriptor, capturedContext.Endpoint);
            Assert.IsNull(capturedContext.Route);
            Assert.AreEqual("edge-01", capturedContext.RouteValues["target"]);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
        }

        [Test]
        public void NotifyObservers_RerunsRouteObserveHandler()
        {
            var calls = 0;
            var endpointDescriptor = new CoapEndpoint(
                Method.GET,
                "diagnostics/{target}/status",
                context =>
                {
                    calls++;
                    return new ValueTask<CoapRouteResult>(
                        CoapRouteResult.Text(StatusCode.Content, "value-" + calls)
                            .WithObserve(calls));
                },
                new object[] { new CoapObserveAttribute(), new CoapProducesAttribute(MediaType.TextPlain) },
                "diagnostics status observe");
            var dataSource = new CoapEndpointDataSource(new[] { endpointDescriptor });
            var matcher = new CoapEndpointMatcher(dataSource);
            var registry = new CoapRouteObserveRegistry();
            var endpoint = CreateEndpoint(dataSource, matcher, registry, "edge-01", "status");
            var exchange = CreateExchange(Method.GET);
            exchange.Request.MarkObserve();
            exchange.Request.Token = new byte[] { 0x01 };
            var remote = new ObservingEndpoint(exchange.Request.Source);
            var relation = new ObserveRelation(new CoapConfig(), remote, endpoint, exchange);
            remote.AddObserveRelation(relation);
            exchange.Relation = relation;

            endpoint.HandleRequest(exchange);

            Assert.IsTrue(relation.Established);
            Assert.AreEqual(1, calls);
            Assert.AreEqual("value-1", exchange.SentResponse.PayloadString);

            Assert.AreEqual(1, registry.NotifyObservers("diagnostics/edge-01/status"));

            Assert.AreEqual(2, calls);
            Assert.AreEqual("value-2", exchange.SentResponse.PayloadString);
            Assert.AreEqual(2, exchange.SentResponse.Observe);
        }

        [Test]
        public void HandleRequest_SetsRouteContextOnResourceBase_AndClearsItAfterInvocation()
        {
            var resources = new List<DiagnosticsCoapResource>();
            var endpoint = CreateEndpoint(CoapRoute.Post(
                "diagnostics/{target}/ping",
                () =>
                {
                    var resource = new DiagnosticsCoapResource();
                    resources.Add(resource);
                    return resource;
                },
                resource => resource.PingAsync()), "edge-01", "ping");
            var exchange = CreateExchange(Method.POST);

            endpoint.HandleRequest(exchange);

            var invokedResource = resources.Single();
            Assert.IsTrue(invokedResource.ContextWasAvailable);
            Assert.AreEqual(Method.POST, invokedResource.MethodSeen);
            Assert.AreEqual("edge-01", invokedResource.TargetSeen);
            Assert.AreEqual(StatusCode.Changed, exchange.SentResponse.StatusCode);
            Assert.Throws<InvalidOperationException>(() =>
            {
                var ignored = invokedResource.Context;
            });
        }

        [Test]
        public void HandleRequest_HandlerException_ReturnsStableServerError()
        {
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/status", _ =>
            {
                throw new InvalidOperationException("handler failed");
            }), "edge-01", "status");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.InternalServerError, exchange.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route handler failed.", exchange.SentResponse.PayloadString);
        }

        [Test]
        public void HandleRequest_NullResult_ReturnsStableServerError()
        {
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/status", _ =>
                new ValueTask<CoapRouteResult>((CoapRouteResult)null)), "edge-01", "status");
            var exchange = CreateExchange(Method.GET);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.InternalServerError, exchange.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route handler returned no result.", exchange.SentResponse.PayloadString);
        }

        [Test]
        public void HandleRequest_MethodMismatch_ReturnsMethodNotAllowed()
        {
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/status", _ =>
                new ValueTask<CoapRouteResult>(CoapRouteResult.Changed())), "edge-01", "status");
            var exchange = CreateExchange(Method.POST);

            endpoint.HandleRequest(exchange);

            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(StatusCode.MethodNotAllowed, exchange.SentResponse.StatusCode);
            Assert.AreEqual("CoAP route method is not allowed.", exchange.SentResponse.PayloadString);
        }

        private static IResource CreateEndpoint(CoapRoute route, params string[] childSegments)
        {
            IResource endpoint = CoapRouteEndpoint.Create(new[] { route }).Single();
            foreach (var segment in childSegments)
            {
                endpoint = endpoint.GetChild(segment);
                Assert.IsNotNull(endpoint);
            }

            return endpoint;
        }

        private static IResource CreateEndpoint(ICoapEndpointDataSource dataSource, params string[] childSegments)
        {
            IResource endpoint = CoapRouteEndpoint.Create(dataSource).Single();
            foreach (var segment in childSegments)
            {
                endpoint = endpoint.GetChild(segment);
                Assert.IsNotNull(endpoint);
            }

            return endpoint;
        }

        private static IResource CreateEndpoint(
            ICoapEndpointDataSource dataSource,
            ICoapEndpointMatcher matcher,
            CoapRouteObserveRegistry registry,
            params string[] childSegments)
        {
            IResource endpoint = CoapRouteEndpoint.Create(dataSource, matcher, null, registry).Single();
            foreach (var segment in childSegments)
            {
                endpoint = endpoint.GetChild(segment);
                Assert.IsNotNull(endpoint);
            }

            return endpoint;
        }

        private static CapturingExchange CreateExchange(Method method)
        {
            var request = new Request(method)
            {
                Source = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 56830)
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

        private sealed class DiagnosticsCoapResource : CoapResourceBase
        {
            public bool ContextWasAvailable { get; private set; }

            public Method MethodSeen { get; private set; }

            public string TargetSeen { get; private set; }

            public ValueTask<CoapRouteResult> PingAsync()
            {
                ContextWasAvailable = Context != null;
                MethodSeen = Method;
                TargetSeen = RouteValues["target"];
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            }
        }
    }
}
