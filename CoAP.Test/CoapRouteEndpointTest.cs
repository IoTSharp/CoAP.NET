using CoAP.Net;
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
            var payload = Encoding.UTF8.GetBytes("{\"ok\":true}");
            var eTag = new byte[] { 0x01, 0x02, 0x03 };
            var endpoint = CreateEndpoint(CoapRoute.Get("diagnostics/{target}/status", context =>
            {
                Assert.AreEqual("edge-01", context.RouteValues["target"]);
                return new ValueTask<CoapRouteResult>(
                    CoapRouteResult.Content(payload, MediaType.ApplicationJson)
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
