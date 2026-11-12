using System;
using System.Net;
using System.Reflection;
using NUnit.Framework;

using CoAP.Net;

namespace CoAP
{
    [TestFixture]
    public sealed class MatcherProtocolTest
    {
        private static readonly IPEndPoint LocalEndpoint =
            new IPEndPoint(IPAddress.Loopback, 51000);
        private static readonly IPEndPoint RemoteEndpoint =
            new IPEndPoint(IPAddress.Loopback, 5683);
        private static readonly IPEndPoint OtherRemoteEndpoint =
            new IPEndPoint(IPAddress.Loopback, 5684);

        [Test]
        public void SeparateResponseFromDifferentEndpointDoesNotMatchToken()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1201, false, RemoteEndpoint);
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                Response response = CreateResponse(request, MessageType.NON, 0x2201, OtherRemoteEndpoint);

                Assert.That(ReceiveResponse(matcher, response), Is.Null);
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void PiggybackedResponseRequiresRequestMessageId()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1202, false, RemoteEndpoint);
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                Response response = CreateResponse(request, MessageType.ACK, 0x2202, RemoteEndpoint);

                Assert.That(ReceiveResponse(matcher, response), Is.Null);
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void SeparateResponseFromRequestEndpointMatchesToken()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1203, false, RemoteEndpoint);
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                Response response = CreateResponse(request, MessageType.NON, 0x2203, RemoteEndpoint);

                Assert.That(ReceiveResponse(matcher, response), Is.SameAs(exchange));
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void MulticastResponseMayComeFromDifferentEndpoint()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(
                    0x1204,
                    true,
                    new IPEndPoint(IPAddress.Parse("239.255.0.1"), 5683));
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                Response response = CreateResponse(request, MessageType.NON, 0x2204, OtherRemoteEndpoint);

                Assert.That(ReceiveResponse(matcher, response), Is.SameAs(exchange));
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void EqualTokensForDifferentPeersRemainIndependent()
        {
            object matcher = CreateMatcher();
            try
            {
                Request first = CreateRequest(0x1205, false, RemoteEndpoint);
                Request second = CreateRequest(0x1206, false, OtherRemoteEndpoint);
                Exchange firstExchange = new Exchange(first, Origin.Local);
                Exchange secondExchange = new Exchange(second, Origin.Local);
                Invoke(matcher, "SendRequest", firstExchange, first);
                Invoke(matcher, "SendRequest", secondExchange, second);

                Assert.That(ReceiveResponse(matcher,
                    CreateResponse(first, MessageType.NON, 0x2205, RemoteEndpoint)),
                    Is.SameAs(firstExchange));
                firstExchange.Complete = true;

                Assert.That(ReceiveResponse(matcher,
                    CreateResponse(second, MessageType.NON, 0x2206, OtherRemoteEndpoint)),
                    Is.SameAs(secondExchange));
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void EqualTokenHashesDoNotMatchDifferentTokenBytes()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1207, false, RemoteEndpoint);
                request.Token = new Byte[] { 0x5c, 0x09, 0x00, 0x00, 0x00 };
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                Response response = CreateResponse(request, MessageType.NON, 0x2207, RemoteEndpoint);
                response.Token = new Byte[] { 0x3f, 0xbc, 0x00, 0x00, 0x00 };

                Assert.That(CoAP.Util.ByteArrayUtils.ComputeHash(request.Token),
                    Is.EqualTo(CoAP.Util.ByteArrayUtils.ComputeHash(response.Token)));
                Assert.That(ReceiveResponse(matcher, response), Is.Null);
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [TestCase(MessageType.ACK)]
        [TestCase(MessageType.RST)]
        public void EmptyMessageFromWrongEndpointDoesNotConsumePendingMessage(MessageType type)
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1208, false, RemoteEndpoint);
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);

                EmptyMessage wrong = new EmptyMessage(type);
                wrong.ID = request.ID;
                wrong.Source = OtherRemoteEndpoint;
                Assert.That(Invoke(matcher, "ReceiveEmptyMessage", wrong), Is.Null);

                EmptyMessage correct = new EmptyMessage(type);
                correct.ID = request.ID;
                correct.Source = RemoteEndpoint;
                Assert.That(Invoke(matcher, "ReceiveEmptyMessage", correct), Is.SameAs(exchange));
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void EmptyAcknowledgementMatchesSentResponseDestination()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1209, false, RemoteEndpoint);
                request.Source = RemoteEndpoint;
                Exchange exchange = new Exchange(request, Origin.Remote);
                Response response = CreateResponse(request, MessageType.CON, 0x2209, LocalEndpoint);
                response.Destination = RemoteEndpoint;
                exchange.CurrentResponse = response;
                Invoke(matcher, "SendResponse", exchange, response);

                EmptyMessage wrong = new EmptyMessage(MessageType.ACK);
                wrong.ID = response.ID;
                wrong.Source = OtherRemoteEndpoint;
                Assert.That(Invoke(matcher, "ReceiveEmptyMessage", wrong), Is.Null);

                EmptyMessage correct = new EmptyMessage(MessageType.ACK);
                correct.ID = response.ID;
                correct.Source = RemoteEndpoint;
                Assert.That(Invoke(matcher, "ReceiveEmptyMessage", correct), Is.SameAs(exchange));
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        [Test]
        public void DuplicateMessageIdWithDifferentTokenDoesNotMatchEarlierResponse()
        {
            object matcher = CreateMatcher();
            try
            {
                Request request = CreateRequest(0x1210, false, RemoteEndpoint);
                Exchange exchange = new Exchange(request, Origin.Local);
                Invoke(matcher, "SendRequest", exchange, request);
                Response response = CreateResponse(request, MessageType.NON, 0x2210, RemoteEndpoint);
                Assert.That(ReceiveResponse(matcher, response), Is.SameAs(exchange));

                Response unexpected = CreateResponse(request, MessageType.NON, response.ID, RemoteEndpoint);
                unexpected.Token = new Byte[] { 0x07, 0x08, 0x09, 0x0a };

                Assert.That(ReceiveResponse(matcher, unexpected), Is.Null);
            }
            finally
            {
                ((IDisposable)matcher).Dispose();
            }
        }

        private static object CreateMatcher()
        {
            Type matcherType = typeof(CoapClient).Assembly.GetType("CoAP.Net.Matcher", true);
            return Activator.CreateInstance(matcherType, new CoapConfig());
        }

        private static Request CreateRequest(Int32 id, Boolean multicast, System.Net.EndPoint destination)
        {
            Request request = Request.NewGet();
            request.ID = id;
            request.Token = new Byte[] { 0x01, 0x02, 0x03, 0x04 };
            request.Source = LocalEndpoint;
            request.Destination = destination;
            request.Multicast = multicast;
            return request;
        }

        private static Response CreateResponse(Request request, MessageType type, Int32 id, System.Net.EndPoint source)
        {
            Response response = Response.CreateResponse(request, StatusCode.Content);
            response.Type = type;
            response.ID = id;
            response.Source = source;
            response.Destination = LocalEndpoint;
            return response;
        }

        private static Exchange ReceiveResponse(object matcher, Response response)
        {
            return (Exchange)Invoke(matcher, "ReceiveResponse", response);
        }

        private static object Invoke(object target, String methodName, params object[] arguments)
        {
            return target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                .Invoke(target, arguments);
        }
    }
}
