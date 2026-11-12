using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;
using NetworkEndPoint = System.Net.EndPoint;

namespace CoAP
{
    [TestFixture]
    public sealed class EndpointBlockwiseLifecycleTest
    {
        [TestCase("Stop")]
        [TestCase("Clear")]
        [TestCase("Dispose")]
        public void LifecycleAction_CancelsPendingUploadExactlyOnce(String action)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Request request = Upload();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Client(endpoint, config).SendAsync(request, response => Assert.Fail("Partial upload completed."), failures.Enqueue);
                Assert.That(channel.Sent.Count, Is.EqualTo(1));

                Act(endpoint, action);
                Act(endpoint, action);

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(request.WaitForResponse(0), Is.Null);
                Assert.That(failures.ToArray(), Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
                Assert.That(channel.Sent.Count, Is.EqualTo(1));
            }
        }

        [TestCase("Stop")]
        [TestCase("Clear")]
        public void StopRestartOrClear_AllowsFreshUpload(String action)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = Client(endpoint, config);
                Request old = Upload();
                client.SendAsync(old);
                Act(endpoint, action);
                Assert.That(old.IsCancelled, Is.True);
                if (action == "Stop")
                    endpoint.Start();

                Request next = Upload();
                client.SendAsync(next);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Assert.That(next.IsCancelled, Is.False);
                Request block = (Request)Spec.Decode(channel.Sent[1]);
                Assert.That(block.Block1.NUM, Is.Zero);
                next.Cancel();
            }
        }

        [Test]
        public void ReservedSzxAck_IsIgnoredAndOriginalDeadlineReportsTimeoutOnce()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            config.BlockwiseStatusLifetime = 75;
            using (ManualResetEventSlim failureReceived = new ManualResetEventSlim())
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = Client(endpoint, config);
                Request request = Upload();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                client.SendAsync(request, response => Assert.Fail("Invalid ACK completed upload."), reason =>
                {
                    failures.Enqueue(reason);
                    failureReceived.Set();
                });
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response invalid = Response.CreateResponse(sent, StatusCode.Continue);
                invalid.Type = MessageType.ACK;
                invalid.ID = sent.ID;
                invalid.SetBlock1(7, true, 0);
                channel.Emit(Spec.Encode(invalid), new IPEndPoint(IPAddress.Loopback, 5683));

                Assert.That(request.IsCancelled, Is.False);
                Assert.That(failures.All(reason => reason == CoapClient.FailReason.TimedOut), Is.True);
                Assert.That(failureReceived.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(request.IsTimedOut, Is.True);
                Assert.That(request.WaitForResponse(0), Is.Null);
                // Cleanup cancellation after a timeout must not report another failure.
                request.Cancel();
                Assert.That(failures.ToArray(), Is.EqualTo(new[] { CoapClient.FailReason.TimedOut }));
            }
        }

        [TestCase(CoapClient.FailReason.Rejected)]
        [TestCase(CoapClient.FailReason.TimedOut)]
        [TestCase(CoapClient.FailReason.Cancelled)]
        public void FirstTerminalFailure_IsReportedOnce(CoapClient.FailReason first)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Request request = Request.NewGet();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Client(endpoint, config).SendAsync(request, fail: failures.Enqueue);
                if (first == CoapClient.FailReason.Rejected)
                    request.IsRejected = true;
                else if (first == CoapClient.FailReason.TimedOut)
                    request.IsTimedOut = true;
                else
                    request.Cancel();
                request.IsRejected = true;
                request.IsTimedOut = true;
                request.Cancel();

                Assert.That(failures.ToArray(), Is.EqualTo(new[] { first }));
            }
        }


        [TestCase(false)]
        [TestCase(true)]
        public void QueuedWorkAfterStopOrDispose_DoesNotResurrectTransfer(Boolean dispose)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            QueuedExecutor executor = new QueuedExecutor();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = executor })
            {
                endpoint.Start();
                Request request = Upload();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Client(endpoint, config).SendAsync(request, fail: failures.Enqueue);
                Int32 requestsReceived = 0;
                endpoint.ReceivingRequest += (sender, e) => requestsReceived++;
                Request incoming = Request.NewGet();
                incoming.ID = 42;
                incoming.Token = new Byte[] { 9 };
                incoming.URI = new Uri("coap://127.0.0.1:5683/value");
                channel.Emit(Spec.Encode(incoming), new IPEndPoint(IPAddress.Loopback, 5683));
                if (dispose) endpoint.Dispose();
                else endpoint.Stop();

                Assert.DoesNotThrow(executor.RunBatch);
                Assert.That(channel.Sent, Is.Empty);
                Assert.That(requestsReceived, Is.Zero);
                Assert.That(request.IsCancelled, Is.True);
                Assert.That(failures.ToArray(), Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
            }
        }


        [TestCase("Clear", false)]
        [TestCase("Stop", false)]
        [TestCase("Dispose", false)]
        [TestCase("Cancel", false)]
        [TestCase("Timeout", false)]
        [TestCase("Timeout", true)]
        public void RespondingTermination_DoesNotContinueOrDeliver(String termination, Boolean finalBlock)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Request request = Request.NewGet();
                request.Responding += (sender, e) =>
                {
                    if (termination == "Cancel") request.Cancel();
                    else if (termination == "Timeout") request.IsTimedOut = true;
                    else Act(endpoint, termination);
                };
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Int32 responses = 0;
                Client(endpoint, config).SendAsync(request, response => responses++, failures.Enqueue);
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response reply = Response.CreateResponse(sent, StatusCode.Content);
                reply.Type = MessageType.ACK;
                reply.ID = sent.ID;
                reply.SetBlock2(0, !finalBlock, 0);
                reply.SetPayload(new Byte[finalBlock ? 1 : 16], MediaType.ApplicationOctetStream);
                channel.Emit(Spec.Encode(reply), new IPEndPoint(IPAddress.Loopback, 5683));

                Assert.That(channel.Sent.Count, Is.EqualTo(1), "A terminal callback must not send a continuation.");
                Assert.That(responses, Is.Zero, "A failed operation must not subsequently report success.");
                Assert.That(request.WaitForResponse(0), Is.Null);
                Assert.That(failures.ToArray(), Is.EqualTo(new[] {
                    termination == "Timeout" ? CoapClient.FailReason.TimedOut : CoapClient.FailReason.Cancelled }));
            }
        }

        [Test]
        public void SuccessfulUnicast_DoesNotReportLateCancellationAsFailure()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Request request = Request.NewGet();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Int32 responses = 0;
                Client(endpoint, config).SendAsync(request, response => responses++, failures.Enqueue);
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response reply = Response.CreateResponse(sent, StatusCode.Content);
                reply.Type = MessageType.ACK;
                reply.ID = sent.ID;
                channel.Emit(Spec.Encode(reply), new IPEndPoint(IPAddress.Loopback, 5683));
                request.Cancel();

                Assert.That(responses, Is.EqualTo(1));
                Assert.That(failures, Is.Empty);
            }
        }


        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleGeneration_DropsOldQueuedWorkAcrossRestart(Boolean clear)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            QueuedExecutor executor = new QueuedExecutor();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = executor })
            {
                endpoint.Start();
                Request request = Upload();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Client(endpoint, config).SendAsync(request, fail: failures.Enqueue);
                Int32 received = 0;
                endpoint.ReceivingRequest += (sender, e) => received++;
                Request incoming = Request.NewGet();
                incoming.ID = 42;
                incoming.Token = new Byte[] { 8 };
                incoming.URI = new Uri("coap://127.0.0.1:5683/value");
                channel.Emit(Spec.Encode(incoming), new IPEndPoint(IPAddress.Loopback, 5683));
                if (clear) endpoint.Clear();
                else { endpoint.Stop(); endpoint.Start(); }

                Assert.DoesNotThrow(executor.RunBatch);
                Assert.That(channel.Sent, Is.Empty);
                Assert.That(received, Is.Zero);
                Assert.That(request.IsCancelled, Is.True);
                Assert.That(failures.ToArray(), Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObserveFallbackOrDeregistration_CompletesAsUnicast(Boolean deregister)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Request request = Request.NewGet();
                if (deregister) request.MarkObserveCancel();
                else request.MarkObserve();
                ConcurrentQueue<CoapClient.FailReason> failures = new ConcurrentQueue<CoapClient.FailReason>();
                Int32 responses = 0;
                Client(endpoint, config).SendAsync(request, response => responses++, failures.Enqueue);
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response reply = Response.CreateResponse(sent, StatusCode.Content);
                reply.Type = MessageType.ACK;
                reply.ID = sent.ID;
                channel.Emit(Spec.Encode(reply), new IPEndPoint(IPAddress.Loopback, 5683));
                request.Cancel();

                Assert.That(responses, Is.EqualTo(1));
                Assert.That(failures, Is.Empty);
            }
        }


        [Test]
        public void ClearFailureCallback_CanStartANewRequestWithoutLosingMatcherState()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = Client(endpoint, config);
                Int32 recovered = 0;
                Request replacement = null;
                client.SendAsync(Upload(), fail: reason =>
                {
                    Assert.That(reason, Is.EqualTo(CoapClient.FailReason.Cancelled));
                    replacement = Request.NewGet();
                    client.SendAsync(replacement, response => recovered++, failure => Assert.Fail(failure.ToString()));
                });
                endpoint.Clear();
                Assert.That(replacement, Is.Not.Null);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Request sent = (Request)Spec.Decode(channel.Sent[1]);
                Response reply = Response.CreateResponse(sent, StatusCode.Content);
                reply.Type = MessageType.ACK;
                reply.ID = sent.ID;
                channel.Emit(Spec.Encode(reply), new IPEndPoint(IPAddress.Loopback, 5683));

                Assert.That(recovered, Is.EqualTo(1));
            }
        }


        [Test]
        public void DisposeStillReleasesTransportWhenCallerFailureCallbackThrows()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = Config();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                Client(endpoint, config).SendAsync(Upload(), fail: reason => throw new InvalidOperationException("host failure"));
                Exception failure = Assert.Catch(() => endpoint.Dispose());
                Assert.That(failure.ToString(), Does.Contain("host failure"));
                Assert.That(endpoint.Running, Is.False);
                Assert.That(channel.IsDisposed, Is.True);
                Assert.Throws<ObjectDisposedException>(() => endpoint.Start());
            }
        }

        private sealed class QueuedExecutor : IExecutor
        {
            private readonly Queue<Action> _tasks = new Queue<Action>();
            public void Start(Action task) { _tasks.Enqueue(task); }
            public void Start(Action<Object> task, Object state) { _tasks.Enqueue(() => task(state)); }
            public void RunBatch()
            {
                Int32 count = Math.Min(_tasks.Count, 32);
                System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
                for (Int32 index = 0; index < count && elapsed.Elapsed < TimeSpan.FromSeconds(5); index++)
                    _tasks.Dequeue()();
                Assert.That(_tasks, Is.Empty);
            }
        }

        private static CoapClient Client(CoAPEndPoint endpoint, CoapConfig config)
        {
            return new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
        }

        private static Request Upload()
        {
            Request request = Request.NewPost();
            request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
            return request;
        }

        private static CoapConfig Config()
        {
            return new CoapConfig { MaxMessageSize = 16, DefaultBlockSize = 16, AckTimeout = 10000, BlockwiseStatusLifetime = 10000 };
        }

        private static void Act(CoAPEndPoint endpoint, String action)
        {
            if (action == "Stop") endpoint.Stop();
            else if (action == "Clear") endpoint.Clear();
            else endpoint.Dispose();
        }

        private sealed class RecordingChannel : IChannel
        {
            public Boolean IsDisposed { get; private set; }
            public event EventHandler<DataReceivedEventArgs> DataReceived;
            public List<Byte[]> Sent { get; } = new List<Byte[]>();
            public NetworkEndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 10000);
            public void Start() { }
            public void Stop() { }
            public void Dispose() { IsDisposed = true; }
            public void Send(Byte[] data, NetworkEndPoint destination) { Sent.Add(data.ToArray()); }
            public void Emit(Byte[] data, NetworkEndPoint source) { DataReceived?.Invoke(this, new DataReceivedEventArgs(data, source)); }
        }
    }
}
