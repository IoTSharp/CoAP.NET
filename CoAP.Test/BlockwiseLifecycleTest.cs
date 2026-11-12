using System;
using System.Runtime.CompilerServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Net;
using CoAP.Channel;
using CoAP.Threading;
using System.Threading;
using System.Threading.Tasks;
using CoAP.Net;
using CoAP.Stack;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class BlockwiseLifecycleTest
    {
        [Test]
        public void ErrorWithoutBlockOptions_ReleasesUploadState()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                Response error = new Response(StatusCode.BadRequest);
                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, error);
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
                Assert.That(transfer.Next.Response, Is.SameAs(error));
            }
        }

        [Test]
        public void FinalBlock2_ReleasesAssemblerAndDoesNotRetainPreviousPayload()
        {
            using (Transfer transfer = new Transfer())
            {
                Response first = new Response(StatusCode.Content);
                first.SetPayload("0123456789abcdef", MediaType.TextPlain);
                first.SetBlock2(0, true, 0);
                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, first);
                Response last = new Response(StatusCode.Content);
                last.SetPayload("done", MediaType.TextPlain);
                last.SetBlock2(0, false, 1);
                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, last);
                Assert.That(transfer.Next.Response.PayloadString, Is.EqualTo("0123456789abcdefdone"));
                Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelOrExchangeCompletion_ReleasesUploadState(bool complete)
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                if (complete)
                    transfer.Exchange.Complete = true;
                else
                    transfer.Request.Cancel();
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.Complete, Is.True);
            }
        }

        [Test]
        public void BlockwiseDeadline_NotifiesOriginalCallerExactlyOnce()
        {
            using (Transfer transfer = new Transfer(40))
            using (ManualResetEventSlim timedOut = new ManualResetEventSlim())
            {
                int failures = 0;
                transfer.Request.TimedOut += (sender, e) =>
                {
                    Interlocked.Increment(ref failures);
                    timedOut.Set();
                };
                transfer.StartUpload();

                Assert.That(timedOut.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(transfer.Request.IsTimedOut, Is.True);
                Assert.That(transfer.Exchange.TimedOut, Is.True);
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
                Assert.That(failures, Is.EqualTo(1));
            }
        }

        [Test]
        public void LayerDispose_CancelsPendingOriginalRequestAndReleasesState()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                int cancellations = 0;
                transfer.Request.Cancelled += (sender, e) => cancellations++;
                IDisposable disposable = transfer.Layer as IDisposable;
                Assert.That(disposable, Is.Not.Null);
                disposable.Dispose();
                disposable.Dispose();
                Assert.That(transfer.Request.IsCancelled, Is.True);
                Assert.That(transfer.Exchange.Complete, Is.True);
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(cancellations, Is.EqualTo(1));
            }
        }

        [Test]
        public void ExchangeTimeout_NotifiesOriginalCallerAndReleasesState()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                int failures = 0;
                transfer.Request.TimedOut += (sender, e) => failures++;
                transfer.Exchange.TimedOut = true;
                Assert.That(transfer.Request.IsTimedOut, Is.True);
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
            }
        }

        [Test]
        public void FinalResponse_RemovesDeadlineAndDoesNotTimeoutLater()
        {
            using (Transfer transfer = new Transfer(40))
            using (ManualResetEventSlim timedOut = new ManualResetEventSlim())
            {
                transfer.Request.TimedOut += (sender, e) => timedOut.Set();
                transfer.StartUpload();
                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, new Response(StatusCode.BadRequest));
                Assert.That(timedOut.Wait(TimeSpan.FromMilliseconds(200)), Is.False);
                Assert.That(transfer.Request.IsTimedOut, Is.False);
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
            }
        }

        [Test]
        public void Clear_IsIdempotentAndAllowsAnotherTransfer()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                int cancellations = 0;
                transfer.Request.Cancelled += (sender, e) => cancellations++;
                transfer.Layer.Clear();
                transfer.Layer.Clear();
                Assert.That(cancellations, Is.EqualTo(1));
                Request second = Request.NewPost();
                second.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
                Exchange next = new Exchange(second, Origin.Local) { Request = second };
                transfer.Layer.SendRequest(transfer.Next, next, second);
                Assert.That(next.RequestBlockStatus, Is.Not.Null);
                transfer.Layer.Clear();
                Assert.That(second.IsCancelled, Is.True);
                Assert.That(next.RequestBlockStatus, Is.Null);
            }
        }

        [Test]
        public void ConcurrentClearAndDispose_CancelEachOwnedExchangeOnce()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                int cancellations = 0;
                transfer.Request.Cancelled += (sender, e) => Interlocked.Increment(ref cancellations);
                Task[] cleanup =
                {
                    Task.Run(() => transfer.Layer.Clear()),
                    Task.Run(() => transfer.Layer.Dispose()),
                    Task.Run(() => transfer.Layer.Clear())
                };
                Assert.That(Task.WaitAll(cleanup, TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(cancellations, Is.EqualTo(1));
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.Complete, Is.True);
            }
        }

        [Test]
        public void Cancellation_DetachesExchangeFromALivingLayer()
        {
            using (ExposedLayer layer = new ExposedLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16,
                BlockwiseStatusLifetime = 60000
            }))
            {
                WeakReference exchange = CreateCancelledExchange(layer);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Assert.That(exchange.IsAlive, Is.False, "A cancelled exchange must not stay rooted by the layer or its timer.");
                GC.KeepAlive(layer);
            }
        }

        [Test]
        public void Cancellation_AllowsOriginalRequestToBeCollectedWhileEndpointRemainsAlive()
        {
            CoapConfig config = new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16,
                AckTimeout = 5,
                AckRandomFactor = 1,
                MaxRetransmit = -1
            };
            using (CoAPEndPoint endpoint = new CoAPEndPoint(new DiscardingChannel(), config)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                WeakReference request = CreateCancelledEndpointRequest(endpoint);
                Stopwatch budget = Stopwatch.StartNew();
                for (int attempt = 0; attempt < 6 && request.IsAlive && budget.Elapsed < TimeSpan.FromSeconds(1); attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    if (request.IsAlive)
                        Thread.Sleep(20);
                }
                Assert.That(request.IsAlive, Is.False, "Endpoint indices and blockwise timers must not keep a cancelled request alive.");
                GC.KeepAlive(endpoint);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateCancelledEndpointRequest(CoAPEndPoint endpoint)
        {
            Request request = Request.NewPost();
            request.EndPoint = endpoint;
            request.Destination = new IPEndPoint(IPAddress.Loopback, 5683);
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            endpoint.SendRequest(request);
            request.Cancel();
            return new WeakReference(request);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateCancelledExchange(ExposedLayer layer)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            layer.SendRequest(new RecordingLayer(), exchange, request);
            request.Cancel();
            return new WeakReference(exchange);
        }

        [Test]
        public void CompletedObserveNotification_ReleasesBodyButAllowsTheNextNotification()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.Request.MarkObserve();
                for (int notification = 0; notification < 2; notification++)
                {
                    Response first = new Response(StatusCode.Content);
                    first.SetPayload("0123456789abcdef", MediaType.TextPlain);
                    first.SetBlock2(0, true, 0);
                    first.Observe = notification;
                    transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, first);
                    Response last = new Response(StatusCode.Content);
                    last.SetPayload(notification.ToString(), MediaType.TextPlain);
                    last.SetBlock2(0, false, 1);
                    transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, last);
                    Assert.That(transfer.Next.Response.PayloadString, Is.EqualTo("0123456789abcdef" + notification));
                    Assert.That(transfer.Next.Response.Observe, Is.EqualTo(notification));
                    Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
                    Assert.That(transfer.Exchange.Complete, Is.False);
                }
                transfer.Request.Cancel();
                Assert.That(transfer.Exchange.Complete, Is.True);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RespondingCallbackTermination_DoesNotSendOrDeliverAfterwards(bool clear, bool more)
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.Request.Responding += (sender, e) =>
                {
                    if (clear) transfer.Layer.Clear();
                    else transfer.Request.Cancel();
                };
                Response response = new Response(StatusCode.Content);
                response.SetPayload("0123456789abcdef", MediaType.TextPlain);
                response.SetBlock2(0, more, 0);

                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, response);

                Assert.That(transfer.Next.SentRequests, Is.Zero);
                Assert.That(transfer.Next.Response, Is.Null);
                Assert.That(transfer.Exchange.ResponseBlockStatus, Is.Null);
                Assert.That(transfer.Exchange.Complete, Is.True);
            }
        }

        [Test]
        public void RespondingCallbackCanWaitForCancellationOnAnotherThread()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.Request.Responding += (sender, e) =>
                    Assert.That(Task.Run(() => transfer.Request.Cancel()).Wait(TimeSpan.FromSeconds(2)), Is.True);
                Response response = new Response(StatusCode.Content);
                response.SetPayload("0123456789abcdef", MediaType.TextPlain);
                response.SetBlock2(0, true, 0);

                transfer.Layer.ReceiveResponse(transfer.Next, transfer.Exchange, response);

                Assert.That(transfer.Next.SentRequests, Is.Zero);
                Assert.That(transfer.Next.Response, Is.Null);
            }
        }

        [Test]
        public void CompletedExchangeCannotStartAnotherUpload()
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.Exchange.Complete = true;
                transfer.Request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
                transfer.Layer.SendRequest(transfer.Next, transfer.Exchange, transfer.Request);
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(transfer.Next.SentRequests, Is.Zero);
            }
        }

        [TestCase("Completed")]
        [TestCase("OriginalCancelled")]
        [TestCase("CurrentCancelled")]
        public void ClearTerminalCallbacks_RunOutsideExchangeLock(string callback)
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                int invoked = 0;
                EventHandler handler = (sender, e) =>
                {
                    invoked++;
                    Assert.That(Monitor.IsEntered(transfer.Exchange), Is.False);
                    Assert.That(Task.Run(() => { lock (transfer.Exchange) { } })
                        .Wait(TimeSpan.FromSeconds(1)), Is.True);
                };
                if (callback == "Completed") transfer.Exchange.Completed += handler;
                else if (callback == "OriginalCancelled") transfer.Request.Cancelled += handler;
                else transfer.Exchange.CurrentRequest.Cancelled += handler;

                transfer.Layer.Clear();

                Assert.That(invoked, Is.EqualTo(1));
            }
        }

        [Test]
        public void TimeoutCompletedCallback_RunsOutsideExchangeLock()
        {
            using (Transfer transfer = new Transfer(40))
            using (ManualResetEventSlim completed = new ManualResetEventSlim())
            {
                bool held = false;
                bool workerAcquired = false;
                transfer.Exchange.Completed += (sender, e) =>
                {
                    held = Monitor.IsEntered(transfer.Exchange);
                    workerAcquired = Task.Run(() => { lock (transfer.Exchange) { } })
                        .Wait(TimeSpan.FromMilliseconds(200));
                    completed.Set();
                };
                transfer.StartUpload();

                Assert.That(completed.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(held, Is.False);
                Assert.That(workerAcquired, Is.True);
            }
        }

        [Test]
        public void DisposedPrepare_PublishesCancellationOutsideCallerLock()
        {
            using (Transfer transfer = new Transfer())
            using (ManualResetEventSlim cancelled = new ManualResetEventSlim())
            {
                transfer.Layer.Dispose();
                Request request = Request.NewPost();
                Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
                bool held = false;
                bool workerAcquired = false;
                request.Cancelled += (sender, e) =>
                {
                    held = Monitor.IsEntered(exchange);
                    workerAcquired = Task.Run(() => { lock (exchange) { } })
                        .Wait(TimeSpan.FromSeconds(1));
                    cancelled.Set();
                };

                lock (exchange)
                    transfer.Layer.ArmCleanup(exchange);

                Assert.That(cancelled.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(held, Is.False);
                Assert.That(workerAcquired, Is.True);
                Assert.That(exchange.Complete, Is.True);
                Assert.That(request.IsCancelled, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearCallbackFailure_StillReleasesEveryTransferAndFragment(bool completedCallback)
        {
            using (Transfer transfer = new Transfer())
            {
                transfer.StartUpload();
                Request second = Request.NewPost();
                second.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
                Exchange secondExchange = new Exchange(second, Origin.Local) { Request = second };
                transfer.Layer.SendRequest(transfer.Next, secondExchange, second);
                Request firstFragment = transfer.Exchange.CurrentRequest;
                Request secondFragment = secondExchange.CurrentRequest;
                int notified = 0;
                EventHandler callback = (sender, e) =>
                {
                    Assert.That(Monitor.IsEntered(transfer.Exchange), Is.False);
                    Assert.That(Monitor.IsEntered(secondExchange), Is.False);
                    if (Interlocked.Increment(ref notified) == 1)
                        throw new InvalidOperationException("caller callback failed");
                };
                if (completedCallback)
                {
                    transfer.Exchange.Completed += callback;
                    secondExchange.Completed += callback;
                }
                else
                {
                    transfer.Request.Cancelled += callback;
                    second.Cancelled += callback;
                }

                Exception error = Assert.Catch(() => transfer.Layer.Clear());

                Assert.That(error.ToString(), Does.Contain("caller callback failed"));
                Assert.That(notified, Is.EqualTo(2));
                Assert.That(transfer.Exchange.RequestBlockStatus, Is.Null);
                Assert.That(secondExchange.RequestBlockStatus, Is.Null);
                Assert.That(firstFragment.IsCancelled, Is.True);
                Assert.That(secondFragment.IsCancelled, Is.True);
                Assert.That(transfer.Exchange.Complete, Is.True);
                Assert.That(secondExchange.Complete, Is.True);
            }
        }

        [Test]
        public void DisposeCallbackFailure_StillUnsubscribesConfiguration()
        {
            CoapConfig config = new CoapConfig { MaxMessageSize = 16, DefaultBlockSize = 16 };
            WeakReference layer = CreateDisposedLayerWithThrowingCallback(config, out Exception error,
                out Exchange first, out Exchange second);

            Assert.That(error, Is.Not.Null);
            Assert.That(error.ToString(), Does.Contain("dispose callback failed"));
            Assert.That(first.RequestBlockStatus, Is.Null);
            Assert.That(second.RequestBlockStatus, Is.Null);
            Assert.That(first.CurrentRequest.IsCancelled, Is.True);
            Assert.That(second.CurrentRequest.IsCancelled, Is.True);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.That(layer.IsAlive, Is.False, "Live configuration must not retain a disposed layer after a callback exception.");
            GC.KeepAlive(config);
            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        [Test]
        public void DisposedPrepare_CallbackFailureStillCancelsFragment()
        {
            using (Transfer transfer = new Transfer())
            using (ManualResetEventSlim fragmentCancelled = new ManualResetEventSlim())
            {
                transfer.Layer.Dispose();
                Request original = Request.NewPost();
                Request fragment = Request.NewPost();
                Exchange exchange = new Exchange(fragment, Origin.Local) { Request = original };
                original.Cancelled += (sender, e) => throw new InvalidOperationException("asynchronous callback failed");
                fragment.Cancelled += (sender, e) => fragmentCancelled.Set();

                lock (exchange)
                    transfer.Layer.ArmCleanup(exchange);

                Assert.That(fragmentCancelled.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(original.IsCancelled, Is.True);
                Assert.That(fragment.IsCancelled, Is.True);
                Assert.That(exchange.Complete, Is.True);
                Assert.That(exchange.RequestBlockStatus, Is.Null);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateDisposedLayerWithThrowingCallback(CoapConfig config,
            out Exception error, out Exchange first, out Exchange second)
        {
            ExposedLayer layer = new ExposedLayer(config);
            Request a = Request.NewPost();
            a.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Request b = Request.NewPost();
            b.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            first = new Exchange(a, Origin.Local) { Request = a };
            second = new Exchange(b, Origin.Local) { Request = b };
            layer.SendRequest(new RecordingLayer(), first, a);
            layer.SendRequest(new RecordingLayer(), second, b);
            int notified = 0;
            EventHandler callback = (sender, e) =>
            {
                if (Interlocked.Increment(ref notified) == 1)
                    throw new InvalidOperationException("dispose callback failed");
            };
            a.Cancelled += callback;
            b.Cancelled += callback;
            error = null;
            try { layer.Dispose(); }
            catch (Exception ex) { error = ex; }
            return new WeakReference(layer);
        }

        private sealed class Transfer : IDisposable
        {
            public Transfer(int lifetime = 60000)
            {
                Request = Request.NewPost();
                Exchange = new Exchange(Request, Origin.Local) { Request = Request };
                Layer = new ExposedLayer(new CoapConfig
                {
                    MaxMessageSize = 16,
                    DefaultBlockSize = 16,
                    BlockwiseStatusLifetime = lifetime
                });
            }

            public Request Request { get; }
            public Exchange Exchange { get; }
            public ExposedLayer Layer { get; }
            public RecordingLayer Next { get; } = new RecordingLayer();

            public void StartUpload()
            {
                Request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
                Layer.SendRequest(Next, Exchange, Request);
                Assert.That(Exchange.RequestBlockStatus, Is.Not.Null);
            }

            public void Dispose()
            {
                Layer.StopTimer(Exchange);
                (Layer as IDisposable)?.Dispose();
            }
        }

        private class ExposedLayer : BlockwiseLayer
        {
            public ExposedLayer(ICoapConfig config) : base(config) { }
            public void StopTimer(Exchange exchange) => ClearBlockCleanup(exchange);
            public void ArmCleanup(Exchange exchange) => PrepareBlockCleanup(exchange);
        }

        private sealed class RecordingLayer : INextLayer
        {
            public int SentRequests { get; private set; }
            public Response Response { get; private set; }
            public void SendRequest(Exchange exchange, Request request) => SentRequests++;
            public void SendResponse(Exchange exchange, Response response) { }
            public void SendEmptyMessage(Exchange exchange, EmptyMessage message) { }
            public void ReceiveRequest(Exchange exchange, Request request) { }
            public void ReceiveResponse(Exchange exchange, Response response) => Response = response;
            public void ReceiveEmptyMessage(Exchange exchange, EmptyMessage message) { }
        }

        private sealed class DiscardingChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived { add { } remove { } }
            public System.Net.EndPoint LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 0);
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
            public void Send(byte[] data, System.Net.EndPoint destination) { }
        }
    }
}
