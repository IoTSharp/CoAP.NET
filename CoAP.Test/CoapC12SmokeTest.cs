using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
#if !NETFX_CORE
using NUnit.Framework;
using TestClass = NUnit.Framework.TestFixtureAttribute;
using TestMethod = NUnit.Framework.TestAttribute;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CoAP.Channel;
using CoAP.Net;
using CoAP.Observe;
using CoAP.Server;
using CoAP.Server.Resources;
using CoAP.Server.Routing;
using CoAP.Threading;

namespace CoAP
{
    [TestClass]
    public class CoapC12SmokeTest
    {
        [TestMethod]
        public void RouteEndpointHandlesBlockwiseLargePayload()
        {
            var config = new CoapConfig
            {
                DefaultBlockSize = 512,
                MaxMessageSize = 512,
            };
            var payload = CreatePayload(32 * 1024);
            byte[] capturedPayload = null;
            string capturedName = null;

            var route = CoapRoute.Post("transfer/{name}", context =>
            {
                capturedName = context.RouteValues["name"];
                capturedPayload = context.Payload.ToArray();
                return new ValueTask<CoapRouteResult>(CoapRouteResult.Changed());
            });

            CoapServer server = null;
            IEndPoint clientEndpoint = null;
            try
            {
                server = CreateRouteServer(config, route);
                clientEndpoint = new CoAPEndPoint(config);
                clientEndpoint.Start();

                var request = new Request(Method.POST);
                request.SetUri("coap://127.0.0.1:" + GetServerPort(server) + "/transfer/firmware");
                request.SetPayload(payload, MediaType.ApplicationOctetStream);
                request.Send(clientEndpoint);

                var response = request.WaitForResponse(10000);

                Assert.IsNotNull(response);
                Assert.AreEqual(StatusCode.Changed, response.StatusCode);
                Assert.AreEqual("firmware", capturedName);
                CollectionAssert.AreEqual(payload, capturedPayload);
            }
            finally
            {
                clientEndpoint?.Dispose();
                server?.Dispose();
            }
        }

        [TestMethod]
        public void RouteEndpointObserveSmokeEstablishesRelation()
        {
            var config = new CoapConfig();
            int? capturedObserve = null;
            var endpoint = new CoapEndpoint(
                Method.GET,
                "sensors/{sensor}/status",
                context =>
                {
                    capturedObserve = context.Observe;
                    return new ValueTask<CoapRouteResult>(
                        CoapRouteResult.Json("{\"status\":\"online\"}")
                            .WithObserve(7)
                            .WithMaxAge(5));
                },
                new object[]
                {
                    new CoapObserveAttribute("status"),
                    new CoapProducesAttribute(MediaType.ApplicationJson),
                    new CoapResourceTitleAttribute("Sensor status"),
                });

            CoapServer server = null;
            IEndPoint clientEndpoint = null;
            try
            {
                server = CreateRouteServer(config, new CoapEndpointDataSource(new[] { endpoint }));
                clientEndpoint = new CoAPEndPoint(config);
                clientEndpoint.Start();

                var request = Request.NewGet();
                request.MarkObserve();
                request.SetUri("coap://127.0.0.1:" + GetServerPort(server) + "/sensors/demo/status");
                request.Send(clientEndpoint);

                var response = request.WaitForResponse(5000);

                Assert.IsNotNull(response);
                Assert.AreEqual(StatusCode.Content, response.StatusCode);
                Assert.AreEqual(0, capturedObserve);
                Assert.IsTrue(response.HasOption(OptionType.Observe));
                Assert.AreEqual(7, response.Observe);
            }
            finally
            {
                clientEndpoint?.Dispose();
                server?.Dispose();
            }
        }

        [TestMethod]
        public void ResultExecutorMarksRouteObserveRelationEstablished()
        {
            var config = new CoapConfig();
            var request = Request.NewGet();
            request.Source = new IPEndPoint(IPAddress.Loopback, 56830);
            request.Token = new byte[] { 0x01 };
            request.MarkObserve();

            var exchange = new CapturingExchange(request);
            var resource = new CapturingResource();
            var remote = new ObservingEndpoint(request.Source);
            var relation = new ObserveRelation(config, remote, resource, exchange);
            exchange.Relation = relation;

            var executor = new CoapResultExecutor();

            executor.ExecuteAsync(
                new CoapResultExecutionContext(exchange, null, null),
                CoapRouteResult.Json("{\"status\":\"online\"}").WithObserve(1))
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.IsTrue(relation.Established);
            Assert.AreSame(relation, resource.LastRelation);
            Assert.IsNotNull(exchange.SentResponse);
            Assert.AreEqual(1, exchange.SentResponse.Observe);
        }

        [TestMethod]
        public void RouteEndpointDtlsPskSmokeReturnsResponse()
        {
            var config = new CoapConfig();
            var keys = new Dictionary<string, string>
            {
                ["device-1"] = "shared-secret",
            };
            var route = CoapRoute.Get("secure/ping", _ =>
                new ValueTask<CoapRouteResult>(
                    CoapRouteResult.Text(StatusCode.Content, "pong")));

            CoapServer server = null;
            IEndPoint clientEndpoint = null;
            try
            {
                server = CreateRouteServer(
                    config,
                    new CoAPEndPoint(new DtlsPskChannel(0, keys, TimeSpan.FromSeconds(15)), config),
                    CoapRouteEndpoint.Create(new[] { route }));

                clientEndpoint = new CoAPEndPoint(
                    new DtlsPskClientChannel(
                        new IPEndPoint(IPAddress.Loopback, 0),
                        "device-1",
                        "shared-secret",
                        TimeSpan.FromSeconds(15)),
                    config);
                clientEndpoint.Start();

                var request = Request.NewGet();
                request.SetUri("coaps://127.0.0.1:" + GetServerPort(server) + "/secure/ping");
                request.Send(clientEndpoint);

                var response = request.WaitForResponse(15000);

                Assert.IsNotNull(response);
                Assert.AreEqual(StatusCode.Content, response.StatusCode);
                Assert.AreEqual("pong", response.PayloadString);
            }
            finally
            {
                clientEndpoint?.Dispose();
                server?.Dispose();
            }
        }

        private static CoapServer CreateRouteServer(CoapConfig config, params CoapRoute[] routes)
        {
            return CreateRouteServer(
                config,
                new CoAPEndPoint(new IPEndPoint(IPAddress.Loopback, 0), config),
                CoapRouteEndpoint.Create(routes));
        }

        private static CoapServer CreateRouteServer(CoapConfig config, CoapEndpointDataSource dataSource)
        {
            return CreateRouteServer(
                config,
                new CoAPEndPoint(new IPEndPoint(IPAddress.Loopback, 0), config),
                CoapRouteEndpoint.Create(dataSource));
        }

        private static CoapServer CreateRouteServer(
            CoapConfig config,
            IEndPoint serverEndpoint,
            IReadOnlyList<IResource> resources)
        {
            var server = new CoapServer(config);
            server.Add(resources.ToArray());
            server.AddEndPoint(serverEndpoint);
            server.Start();
            return server;
        }

        private static int GetServerPort(CoapServer server)
        {
            return ((IPEndPoint)server.EndPoints.Single().LocalEndPoint).Port;
        }

        private static byte[] CreatePayload(int size)
        {
            var payload = new byte[size];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            return payload;
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

        private sealed class CapturingResource : IResource
        {
            private readonly ResourceAttributes _attributes = new ResourceAttributes();

            public string Name { get; set; } = "observe";

            public string Path { get; set; } = string.Empty;

            public string Uri => Path + Name;

            public bool Visible => true;

            public bool Cachable => false;

            public bool Observable => true;

            public ResourceAttributes Attributes => _attributes;

            public IExecutor Executor => null;

            public IEnumerable<IEndPoint> EndPoints => Array.Empty<IEndPoint>();

            public IResource Parent { get; set; }

            public IEnumerable<IResource> Children => Array.Empty<IResource>();

            public ObserveRelation LastRelation { get; private set; }

            public void Add(IResource child)
            {
                throw new NotSupportedException();
            }

            public bool Remove(IResource child)
            {
                return false;
            }

            public IResource GetChild(string name)
            {
                return null;
            }

            public void AddObserveRelation(ObserveRelation relation)
            {
                LastRelation = relation;
            }

            public void RemoveObserveRelation(ObserveRelation relation)
            {
                if (ReferenceEquals(LastRelation, relation))
                {
                    LastRelation = null;
                }
            }

            public void HandleRequest(Exchange exchange)
            {
            }
        }
    }
}
