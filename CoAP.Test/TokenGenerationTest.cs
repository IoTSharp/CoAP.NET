using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CoAP.Net;
using CoAP.Stack;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class TokenGenerationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticToken_UsesEightBytesRegardlessOfOldCounterConfiguration(bool randomStart)
        {
            TokenLayer layer = new TokenLayer(new CoapConfig { UseRandomTokenStart = randomStart });
            Request request = Request.NewGet();

            Send(layer, request);

            Assert.That(request.Token, Has.Length.EqualTo(8));
        }

        [Test]
        public void NewLayers_DoNotRestartAtTheSameDeterministicToken()
        {
            CoapConfig config = new CoapConfig { UseRandomTokenStart = false };
            Request first = Request.NewGet();
            Request second = Request.NewGet();

            Send(new TokenLayer(config), first);
            Send(new TokenLayer(config), second);

            Assert.That(second.Token, Is.Not.EqualTo(first.Token));
            // Detects the old deterministic restart, not the entropy of an RNG.
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(8)]
        public void ExplicitToken_IsPreservedIncludingEmpty(int length)
        {
            TokenLayer layer = new TokenLayer(new CoapConfig());
            byte[] token = new byte[length];
            Request request = Request.NewGet();
            request.Token = token;

            Send(layer, request);

            Assert.That(request.Token, Is.SameAs(token));
        }

        [Test]
        public void ResendingRequest_PreservesTheTokenAndEncodedDatagram()
        {
            TokenLayer layer = new TokenLayer(new CoapConfig());
            Request request = Request.NewGet();
            request.ID = 1234;
            Exchange exchange = Send(layer, request);
            byte[] token = request.Token;
            byte[] datagram = Spec.Encode(request);

            layer.SendRequest(new Sink(), exchange, request);

            Assert.That(request.Token, Is.SameAs(token));
            Assert.That(Spec.Encode(request), Is.EqualTo(datagram));
        }

        [Test]
        public void ConcurrentRequests_AreIndependentAndMatchTheirResponses()
        {
            TokenLayer layer = new TokenLayer(new CoapConfig { UseRandomTokenStart = false });
            ConcurrentDictionary<string, byte> observed = new ConcurrentDictionary<string, byte>();
            using (CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                Parallel.For(0, 256, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellation.Token
                }, number =>
                {
                    Request request = Request.NewGet();
                    Exchange exchange = Send(layer, request);
                    Assert.That(request.Token, Has.Length.EqualTo(8));
                    Assert.That(observed.TryAdd(Convert.ToHexString(request.Token), 0), Is.True);
                    Response response = new Response(StatusCode.Content);
                    layer.SendResponse(new Sink(), exchange, response);
                    Assert.That(response.Token, Is.EqualTo(request.Token));
                });
            }
            Assert.That(observed.Count, Is.EqualTo(256));
            // This is a concurrency/identity smoke check, not a statistical or security proof.
        }

        [Test]
        public void BlockwiseContinuationAndObserveCancellation_PreserveAssignedToken()
        {
            TokenLayer layer = new TokenLayer(new CoapConfig());
            Request registration = Request.NewGet();
            registration.MarkObserve();
            Exchange exchange = Send(layer, registration);
            byte[] token = registration.Token;

            Request continuation = Request.NewGet();
            continuation.SetBlock2(0, false, 1);
            continuation.Token = token;
            exchange.CurrentRequest = continuation;
            layer.SendRequest(new Sink(), exchange, continuation);
            Assert.That(continuation.Token, Is.SameAs(token));

            Request refresh = Request.NewGet();
            refresh.MarkObserve();
            refresh.Token = token;
            layer.SendRequest(new Sink(), exchange, refresh);
            Assert.That(refresh.Token, Is.SameAs(token));

            Request cancellation = Request.NewGet();
            cancellation.MarkObserveCancel();
            cancellation.Token = token;
            layer.SendRequest(new Sink(), exchange, cancellation);
            Assert.That(cancellation.Token, Is.SameAs(token));
        }

        [Test]
        public void CompletedRequests_AreNotRetainedByTheTokenLayer()
        {
            TokenLayer layer = new TokenLayer(new CoapConfig());
            WeakReference request = GenerateCompletedRequest(layer);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(request.IsAlive, Is.False);
            GC.KeepAlive(layer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference GenerateCompletedRequest(TokenLayer layer)
        {
            Request request = Request.NewGet();
            Exchange exchange = Send(layer, request);
            exchange.Complete = true;
            return new WeakReference(request);
        }

        private static Exchange Send(TokenLayer layer, Request request)
        {
            request.Destination = new IPEndPoint(IPAddress.Loopback, 5683);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            layer.SendRequest(new Sink(), exchange, request);
            return exchange;
        }

        private sealed class Sink : INextLayer
        {
            public void SendRequest(Exchange exchange, Request request) { }
            public void SendResponse(Exchange exchange, Response response) { }
            public void SendEmptyMessage(Exchange exchange, EmptyMessage message) { }
            public void ReceiveRequest(Exchange exchange, Request request) { }
            public void ReceiveResponse(Exchange exchange, Response response) { }
            public void ReceiveEmptyMessage(Exchange exchange, EmptyMessage message) { }
        }
    }
}
