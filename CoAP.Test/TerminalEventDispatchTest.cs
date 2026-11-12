using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoAP.Net;
using CoAP.Stack;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class TerminalEventDispatchTest
    {
        [TestCase("Cancelled")]
        [TestCase("TimedOut")]
        [TestCase("Rejected")]
        [TestCase("Completed")]
        public void EarlierThrowingSubscriber_DoesNotPreventLaterCleanup(string terminal)
        {
            Message message = new Message();
            Exchange exchange = new Exchange(Request.NewGet(), Origin.Local);
            object target = terminal == "Completed" ? exchange : message;
            int cleaned = 0;
            Subscribe(terminal, message, exchange, (sender, e) =>
                throw new InvalidOperationException("early public callback failed"));
            Subscribe(terminal, message, exchange, (sender, e) =>
            {
                Assert.That(sender, Is.SameAs(target));
                Assert.That(Monitor.IsEntered(target), Is.False);
                Assert.That(Task.Run(() => { lock (target) { } }).Wait(TimeSpan.FromSeconds(1)), Is.True);
                cleaned++;
            });

            Exception error = Assert.Catch(() => Fire(terminal, message, exchange));

            Assert.That(cleaned, Is.EqualTo(1));
            Assert.That(error, Is.TypeOf<AggregateException>());
            Assert.That(error.ToString(), Does.Contain("early public callback failed"));
        }

        [TestCase("Cancelled")]
        [TestCase("TimedOut")]
        [TestCase("Rejected")]
        [TestCase("Completed")]
        public void MultipleThrowingSubscribers_AreReportedAfterAllHandlersRun(string terminal)
        {
            Message message = new Message();
            Exchange exchange = new Exchange(Request.NewGet(), Origin.Local);
            List<string> called = new List<string>();
            Exception first = new InvalidOperationException("first terminal failure");
            Exception second = new ArgumentException("second terminal failure");
            Subscribe(terminal, message, exchange, (sender, e) => { called.Add("first"); throw first; });
            Subscribe(terminal, message, exchange, (sender, e) => { called.Add("second"); throw second; });
            Subscribe(terminal, message, exchange, (sender, e) => called.Add("cleanup"));

            Exception error = Assert.Catch(() => Fire(terminal, message, exchange));

            Assert.That(called, Is.EqualTo(new[] { "first", "second", "cleanup" }));
            Assert.That(error, Is.TypeOf<AggregateException>());
            Assert.That(((AggregateException)error).Flatten().InnerExceptions,
                Is.EqualTo(new[] { first, second }));
        }

        [TestCase("Cancelled")]
        [TestCase("TimedOut")]
        [TestCase("Rejected")]
        public void EarlierRequestCallbackFailure_DoesNotRetainBlockwiseState(string terminal)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            Subscribe(terminal, request, exchange, (sender, e) =>
                throw new InvalidOperationException("public request callback failed"));
            using (BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            }))
            {
                layer.SendRequest(new DiscardingLayer(), exchange, request);
                Request fragment = exchange.CurrentRequest;
                Assert.That(exchange.RequestBlockStatus, Is.Not.Null);

                Exception error = Assert.Catch(() => Fire(terminal, request, exchange));

                Assert.That(error.ToString(), Does.Contain("public request callback failed"));
                Assert.That(exchange.RequestBlockStatus, Is.Null);
                Assert.That(exchange.ResponseBlockStatus, Is.Null);
                Assert.That(exchange.Complete, Is.True);
                Assert.That(fragment.IsCancelled, Is.True);
            }
        }

        [Test]
        public void EarlierCompletedCallbackFailure_DoesNotRetainBlockwiseState()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            exchange.Completed += (sender, e) => throw new InvalidOperationException("public exchange callback failed");
            using (BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            }))
            {
                layer.SendRequest(new DiscardingLayer(), exchange, request);

                Exception error = Assert.Catch(() => exchange.Complete = true);

                Assert.That(error.ToString(), Does.Contain("public exchange callback failed"));
                Assert.That(exchange.RequestBlockStatus, Is.Null);
                Assert.That(exchange.ResponseBlockStatus, Is.Null);
            }
        }

        [Test]
        public void AcknowledgedEvent_PreservesImmediateExceptionContract()
        {
            Message message = new Message();
            int later = 0;
            message.Acknowledged += (sender, e) => throw new InvalidOperationException("ack callback failed");
            message.Acknowledged += (sender, e) => later++;

            Assert.Throws<InvalidOperationException>(() => message.IsAcknowledged = true);
            Assert.That(later, Is.Zero);
        }

        [Test]
        public void RespondEvent_PreservesImmediateExceptionContract()
        {
            Request request = Request.NewGet();
            int later = 0;
            request.Respond += (sender, e) => throw new InvalidOperationException("respond callback failed");
            request.Respond += (sender, e) => later++;

            Assert.Throws<InvalidOperationException>(() => request.Response = new Response(StatusCode.Content));
            Assert.That(later, Is.Zero);
        }

        private static void Subscribe(string terminal, Message message, Exchange exchange, EventHandler handler)
        {
            switch (terminal)
            {
                case "Cancelled": message.Cancelled += handler; break;
                case "TimedOut": message.TimedOut += handler; break;
                case "Rejected": message.Rejected += handler; break;
                case "Completed": exchange.Completed += handler; break;
                default: throw new ArgumentOutOfRangeException(nameof(terminal));
            }
        }

        private static void Fire(string terminal, Message message, Exchange exchange)
        {
            switch (terminal)
            {
                case "Cancelled": message.Cancel(); break;
                case "TimedOut": message.IsTimedOut = true; break;
                case "Rejected": message.IsRejected = true; break;
                case "Completed": exchange.Complete = true; break;
                default: throw new ArgumentOutOfRangeException(nameof(terminal));
            }
        }

        private sealed class DiscardingLayer : INextLayer
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
