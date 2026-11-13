using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class EndpointOwnedExchangeTest
    {
        private static readonly IPEndPoint Peer = new IPEndPoint(IPAddress.Loopback, 5683);

        [TestCase("Clear")]
        [TestCase("StopStart")]
        [TestCase("Dispose")]
        public void SmallConfirmableRequestIsRetiredWithItsRetransmissionContext(String action)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                endpoint.Start();
                Request request = Request.NewGet();
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                Client(endpoint).SendAsync(request, fail: failures.Add);
                Exchange exchange = ActiveExchange(endpoint);
                Object context = exchange.Get("TransmissionContext");
                Assert.That(context, Is.Not.Null);

                if (action == "Clear") endpoint.Clear();
                else if (action == "Dispose") endpoint.Dispose();
                else { endpoint.Stop(); endpoint.Start(); }

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(exchange.Complete, Is.True);
                Assert.That(exchange.Get("TransmissionContext"), Is.Null);
                Assert.That(failures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
                InvokeTimer(context);
                ((IOutbox)endpoint).SendRequest(exchange, request);
                Assert.That(channel.Sent.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void EstablishedNonBlockwiseObserveReleasesReregistrationContext()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                endpoint.Start();
                Request request = Request.NewGet().MarkObserve();
                Client(endpoint).SendAsync(request);
                Exchange exchange = ActiveExchange(endpoint);
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response response = Response.CreateResponse(sent, StatusCode.Content);
                response.Type = MessageType.ACK;
                response.ID = sent.ID;
                response.Observe = 1;
                response.MaxAge = 60;
                channel.Emit(Spec.Encode(response));
                Object context = exchange.Get("ReregistrationContext");
                Assert.That(context, Is.Not.Null);

                endpoint.Clear();

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(exchange.Get("ReregistrationContext"), Is.Null);
                Assert.That(exchange.Get("TransmissionContext"), Is.Null);
                InvokeTimer(context);
                Assert.That(channel.Sent.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void CleanupCallbackCanStartReplacementRequest()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                endpoint.Start();
                CoapClient client = Client(endpoint);
                Request original = Request.NewGet();
                Request replacement = Request.NewGet();
                client.SendAsync(original, fail: reason => client.SendAsync(replacement));

                endpoint.Clear();

                Assert.That(original.IsCancelled, Is.True);
                Assert.That(replacement.IsCancelled, Is.False);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Assert.That(ActiveExchange(endpoint).Request, Is.SameAs(replacement));
                replacement.Cancel();
            }
        }

        [Test]
        public void ObserveRefreshReleasesBothProtocolContextsOnClear()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel, notificationBackoff: 1))
            {
                endpoint.Start();
                Request request = Request.NewGet().MarkObserve();
                Client(endpoint).SendAsync(request);
                Exchange exchange = ActiveExchange(endpoint);
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response response = Response.CreateResponse(sent, StatusCode.Content);
                response.Type = MessageType.ACK;
                response.ID = sent.ID;
                response.Observe = 1;
                response.MaxAge = 0;
                channel.SentSignal.Reset();
                channel.Emit(Spec.Encode(response));
                Object reregistration = exchange.Get("ReregistrationContext");
                Assert.That(channel.SentSignal.Wait(TimeSpan.FromSeconds(2)), Is.True,
                    "The zero-Max-Age re-registration should send within the bounded test window.");
                Object transmission = exchange.Get("TransmissionContext");
                Assert.That(reregistration, Is.Not.Null);
                Assert.That(transmission, Is.Not.Null);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));

                endpoint.Clear();

                Assert.That(exchange.Get("ReregistrationContext"), Is.Null);
                Assert.That(exchange.Get("TransmissionContext"), Is.Null);
                Assert.That(request.IsCancelled, Is.True);
                InvokeTimer(reregistration);
                InvokeTimer(transmission);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
            }
        }

        private static void InvokeTimer(Object context)
        {
            context.GetType().GetMethod("timer_Elapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(context, new Object[] { null, null });
        }

        [Test]
        public void OrdinaryServerAcknowledgementIsSentAfterExchangeCompletes()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                Exchange received = null;
                endpoint.MessageDeliverer = new RequestDeliverer(exchange =>
                {
                    received = exchange;
                    exchange.SendResponse(new Response(StatusCode.Content));
                });
                endpoint.Start();
                Request request = new Request(Method.GET) { ID = 123, Token = new Byte[] { 9 } };
                channel.Emit(Spec.Encode(request));

                Assert.That(received.Complete, Is.True);
                Response response = (Response)Spec.Decode(channel.Sent.Single());
                Assert.That(response.Type, Is.EqualTo(MessageType.ACK));
                Assert.That(response.ID, Is.EqualTo(request.ID));
            }
        }

        [Test]
        public void PendingServerRequestIsCancelledOnClear()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                Request received = null;
                endpoint.MessageDeliverer = new RequestDeliverer(exchange => received = exchange.Request);
                endpoint.Start();
                Request request = new Request(Method.GET) { ID = 123, Token = new Byte[] { 9 } };
                channel.Emit(Spec.Encode(request));

                endpoint.Clear();

                Assert.That(received.IsCancelled, Is.True);
                Assert.That(channel.Sent, Is.Empty);
            }
        }


        [Test]
        public void ClearReleasesAllBlockwiseStateBeforeAnyPublicCompletionCallback()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = Endpoint(channel))
            {
                List<Exchange> received = new List<Exchange>();
                endpoint.MessageDeliverer = new RequestDeliverer(received.Add);
                endpoint.Start();
                for (Int32 number = 1; number <= 2; number++)
                {
                    Request request = new Request(Method.POST) { ID = number, Token = new Byte[] { (Byte)number } };
                    request.UriPath = "pending-" + number;
                    request.SetBlock1(0, false, 0);
                    request.SetPayload(new Byte[8], MediaType.ApplicationOctetStream);
                    channel.Emit(Spec.Encode(request));
                }
                Assert.That(received.Count, Is.EqualTo(2));
                Assert.That(received.All(exchange => exchange.RequestBlockStatus != null), Is.True);
                received[0].Completed += (sender, args) =>
                {
                    Assert.That(received.All(exchange => exchange.RequestBlockStatus == null &&
                        exchange.ResponseBlockStatus == null), Is.True,
                        "Every old body context must be released before the first user notification.");
                };

                Assert.DoesNotThrow(endpoint.Clear);
            }
        }

        private sealed class RequestDeliverer : IMessageDeliverer
        {
            private readonly Action<Exchange> _request;
            internal RequestDeliverer(Action<Exchange> request) { _request = request; }
            public void DeliverRequest(Exchange exchange) { _request(exchange); }
            public void DeliverResponse(Exchange exchange, Response response) { }
        }

        private static CoAPEndPoint Endpoint(RecordingChannel channel, int notificationBackoff = 10000)
        {
            return new CoAPEndPoint(channel, new CoapConfig { AckTimeout = 10000, NotificationReregistrationBackoff = notificationBackoff })
            { Executor = Executors.NoThreading };
        }

        private static CoapClient Client(CoAPEndPoint endpoint)
        {
            return new CoapClient(new Uri("coap://127.0.0.1:5683/value"), endpoint.Config) { EndPoint = endpoint };
        }

        private static Exchange ActiveExchange(CoAPEndPoint endpoint)
        {
            Object matcher = typeof(CoAPEndPoint).GetField("_matcher", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(endpoint);
            var exchanges = (IDictionary<Exchange.KeyToken, Exchange>)matcher.GetType()
                .GetField("_exchangesByToken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(matcher);
            return exchanges.Values.Single();
        }

        private sealed class RecordingChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived;
            public List<Byte[]> Sent { get; } = new List<Byte[]>();
            public ManualResetEventSlim SentSignal { get; } = new ManualResetEventSlim(false);
            public System.Net.EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 10000);
            public void Start() { }
            public void Stop() { }
            public void Dispose() { SentSignal.Dispose(); }
            public void Send(Byte[] data, System.Net.EndPoint endpoint)
            {
                Sent.Add(data.ToArray());
                SentSignal.Set();
            }
            public void Emit(Byte[] data) { DataReceived?.Invoke(this, new DataReceivedEventArgs(data, Peer)); }
        }
    }
}
