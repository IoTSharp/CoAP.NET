using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoAP.Net;
using CoAP.Stack;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class ObserveLongDeadlineTest
    {
        [TestCase(2147483L)]
        [TestCase(2147484L)]
        [TestCase(4294967295L)]
        public void LegalMaxAgeDoesNotOverflowTimer(long maxAge)
        {
            Request request = Request.NewGet().MarkObserve();
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            ObserveLayer layer = new ObserveLayer(new CoapConfig { NotificationReregistrationBackoff = 0 });
            try
            {
                Response notification = new Response(StatusCode.Content) { Observe = 0, MaxAge = maxAge };
                Assert.DoesNotThrow(() => layer.ReceiveResponse(new RecordingLayer(), exchange, notification));
            }
            finally
            {
                (exchange.Remove("ReregistrationContext") as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void ZeroMaxAgeAndBackoffDoesNotThrow()
        {
            Request request = Request.NewGet().MarkObserve();
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            ObserveLayer layer = new ObserveLayer(new CoapConfig { NotificationReregistrationBackoff = 0 });
            try
            {
                Response notification = new Response(StatusCode.Content) { Observe = 0, MaxAge = 0 };
                Assert.DoesNotThrow(() => layer.ReceiveResponse(new RecordingLayer(), exchange, notification));
            }
            finally
            {
                (exchange.Remove("ReregistrationContext") as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void LaterNotificationReplacesTheMonotonicDeadline()
        {
            FakeTimeProvider clock = new FakeTimeProvider();
            Request request = Request.NewGet().MarkObserve();
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            ObserveLayer layer = new ObserveLayer(new CoapConfig { NotificationReregistrationBackoff = 0 }, clock);
            try
            {
                Response first = new Response(StatusCode.Content) { Observe = 1, MaxAge = 30 };
                layer.ReceiveResponse(new RecordingLayer(), exchange, first);
                Assert.That(clock.Timer.Changes, Is.EqualTo(2));
                Assert.That(clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(30)));

                clock.Advance(TimeSpan.FromSeconds(5));
                Response second = new Response(StatusCode.Content) { Observe = 2, MaxAge = 60 };
                layer.ReceiveResponse(new RecordingLayer(), exchange, second);
                Assert.That(clock.Timer.Changes, Is.EqualTo(3));
                Assert.That(clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(60)));
            }
            finally
            {
                (exchange.Remove("ReregistrationContext") as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void QueuedTimerCallbackIsIgnoredAfterCancellation()
        {
            FakeTimeProvider clock = new FakeTimeProvider();
            Request request = Request.NewGet().MarkObserve();
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            int refreshes = 0;
            ObserveLayer layer = new ObserveLayer(new CoapConfig { NotificationReregistrationBackoff = 0 }, clock);
            try
            {
                layer.ReceiveResponse(new RecordingLayer(), exchange,
                    new Response(StatusCode.Content) { Observe = 1, MaxAge = 1 });
                request.Reregistering += (sender, args) => refreshes++;
                request.Cancel();
                clock.Timer.Fire();
                Assert.That(refreshes, Is.Zero);
                Assert.That(clock.Timer.Disposed, Is.True);
            }
            finally
            {
                (exchange.Remove("ReregistrationContext") as IDisposable)?.Dispose();
            }
        }

        [TestCase(0L)]
        [TestCase(30L)]
        [TestCase(2147483L)]
        [TestCase(2147484L)]
        [TestCase(4294967295L)]
        public void EachTimerSliceStaysWithinPlatformRange(long maxAge)
        {
            using (Observation observation = new Observation(250))
            {
                observation.Notify(maxAge);
                Assert.That(observation.Clock.CreatedTimers, Is.EqualTo(1));
                Assert.That(observation.Clock.Timer.LastDueTime,
                    Is.EqualTo(TimeSpan.FromMilliseconds(Math.Min(maxAge * 1000 + 250, int.MaxValue))));
                Assert.That(observation.Next.SentRequests, Is.Zero);
            }
        }

        [TestCase(1L)]
        [TestCase(2147484L)]
        [TestCase(4294967295L)]
        public void RefreshWaitsUntilTheEntireDeadlineAndRunsOnce(long maxAge)
        {
            using (Observation observation = new Observation(11))
            {
                observation.Notify(maxAge);
                TimeSpan total = TimeSpan.FromMilliseconds(maxAge * 1000 + 11);
                TimeSpan firstSlice = observation.Clock.Timer.LastDueTime;
                observation.Clock.Advance(firstSlice - TimeSpan.FromMilliseconds(1));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                observation.Clock.Advance(total - firstSlice);
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                Assert.That(observation.Clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.FromMilliseconds(1)));

                observation.Clock.Advance(TimeSpan.FromMilliseconds(1));
                observation.Clock.Timer.Fire();
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.EqualTo(1));
                Assert.That(observation.Next.Request.Observe, Is.Zero);
                Assert.That(observation.Next.Request.Token, Is.EqualTo(observation.Request.Token));
                Assert.That(observation.Next.Request.UriPath, Is.EqualTo("/sensors/value"));
                Assert.That(observation.Next.Request.Destination, Is.EqualTo(observation.Request.Destination));
            }
        }

        [Test]
        public void OldQueuedCallbackRechecksTheUpdatedNotificationDeadline()
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(30);
                observation.Clock.Advance(TimeSpan.FromSeconds(5));
                observation.Notify(60);
                observation.Clock.Advance(TimeSpan.FromSeconds(25));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                Assert.That(observation.Clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(35)));
                observation.Clock.Advance(TimeSpan.FromSeconds(35));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.EqualTo(1));
            }
        }

        [Test]
        public void NewNotificationCanShortenTheDeadline()
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(uint.MaxValue);
                observation.Notify(1);
                observation.Clock.Advance(TimeSpan.FromSeconds(1));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.EqualTo(1));
            }
        }

        [Test]
        public void WallClockChangesDoNotExpireTheMonotonicDeadline()
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(60);
                observation.Clock.WallClock = observation.Clock.WallClock.AddYears(200);
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                observation.Clock.WallClock = observation.Clock.WallClock.AddYears(-400);
                observation.Clock.Advance(TimeSpan.FromSeconds(60));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.EqualTo(1));
            }
        }

        [TestCase("Cancel")]
        [TestCase("Timeout")]
        [TestCase("Reject")]
        [TestCase("Complete")]
        public void TerminalStateReleasesTimerAndRejectsQueuedCallbacks(string terminal)
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(uint.MaxValue);
                if (terminal == "Cancel") observation.Request.Cancel();
                else if (terminal == "Timeout") observation.Request.IsTimedOut = true;
                else if (terminal == "Reject") observation.Request.IsRejected = true;
                else observation.Exchange.Complete = true;

                Assert.That(observation.Exchange.Get("ReregistrationContext"), Is.Null);
                Assert.That(observation.Clock.Timer.Disposed, Is.True);
                observation.Clock.Advance(TimeSpan.FromMilliseconds(uint.MaxValue * 1000L));
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
            }
        }

        [Test]
        public void ZeroDeadlineSchedulesImmediateSingleRefresh()
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(0);
                Assert.That(observation.Clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.Zero));
                observation.Clock.Timer.Fire();
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.EqualTo(1));
            }
        }

        [Test]
        public void ResponseWithoutObserveReleasesThePreviousTimer()
        {
            using (Observation observation = new Observation())
            {
                observation.Notify(uint.MaxValue);
                Response final = new Response(StatusCode.Content);
                observation.Layer.ReceiveResponse(observation.Next, observation.Exchange, final);
                Assert.That(observation.Next.Response, Is.SameAs(final));
                Assert.That(observation.Exchange.Get("ReregistrationContext"), Is.Null);
                Assert.That(observation.Clock.Timer.Disposed, Is.True);
            }
        }

        [Test]
        public void CancellationDuringPublicRefreshCallbackRunsOutsideExchangeLock()
        {
            using (Observation observation = new Observation())
            {
                observation.Request.Reregistering += (sender, args) =>
                {
                    Assert.That(Monitor.IsEntered(observation.Exchange), Is.False);
                    Assert.That(Task.Run(() => observation.Request.Cancel()).Wait(TimeSpan.FromSeconds(1)), Is.True);
                };
                observation.Notify(0);
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                Assert.That(observation.Clock.Timer.Disposed, Is.True);
            }
        }

        [Test]
        public void NotificationDuringPublicRefreshCallbackInvalidatesThePendingRefresh()
        {
            using (Observation observation = new Observation())
            {
                observation.Request.Reregistering += (sender, args) => observation.Notify(60);
                observation.Notify(0);
                observation.Clock.Timer.Fire();
                Assert.That(observation.Next.SentRequests, Is.Zero);
                Assert.That(observation.Clock.Timer.LastDueTime, Is.EqualTo(TimeSpan.FromSeconds(60)));
            }
        }

        [Test]
        public void ThrowingRefreshCallbackStillReleasesTheTimer()
        {
            using (Observation observation = new Observation())
            {
                observation.Request.Reregistering += (sender, args) => throw new InvalidOperationException("refresh callback");
                observation.Notify(0);
                Assert.DoesNotThrow(() => observation.Clock.Timer.Fire());
                Assert.That(observation.Clock.Timer.Disposed, Is.True);
                Assert.That(observation.Exchange.Get("ReregistrationContext"), Is.Null);
            }
        }

        private sealed class Observation : IDisposable
        {
            public Observation(int backoff = 0)
            {
                Request = Request.NewGet().MarkObserve();
                Request.Token = new byte[] { 3, 4 };
                Request.UriPath = "sensors/value";
                Request.Destination = new IPEndPoint(IPAddress.Loopback, 5683);
                Exchange = new Exchange(Request, Origin.Local) { Request = Request };
                Layer = new ObserveLayer(new CoapConfig { NotificationReregistrationBackoff = backoff }, Clock);
            }
            public FakeTimeProvider Clock { get; } = new FakeTimeProvider();
            public Request Request { get; }
            public Exchange Exchange { get; }
            public ObserveLayer Layer { get; }
            public RecordingLayer Next { get; } = new RecordingLayer();
            public void Notify(long maxAge) => Layer.ReceiveResponse(Next, Exchange,
                new Response(StatusCode.Content) { Observe = 1, MaxAge = maxAge });
            public void Dispose() => (Exchange.Remove("ReregistrationContext") as IDisposable)?.Dispose();
        }

        private sealed class RecordingLayer : INextLayer
        {
            public int SentRequests { get; private set; }
            public Request Request { get; private set; }
            public Response Response { get; private set; }
            public void SendRequest(Exchange exchange, Request request) { SentRequests++; Request = request; }
            public void SendResponse(Exchange exchange, Response response) { }
            public void SendEmptyMessage(Exchange exchange, EmptyMessage message) { }
            public void ReceiveRequest(Exchange exchange, Request request) { }
            public void ReceiveResponse(Exchange exchange, Response response) => Response = response;
            public void ReceiveEmptyMessage(Exchange exchange, EmptyMessage message) { }
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private long _timestamp;
            public FakeTimer Timer { get; } = new FakeTimer();
            public int CreatedTimers { get; private set; }
            public DateTimeOffset WallClock { get; set; } = new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => WallClock;
            public override long TimestampFrequency => TimeSpan.TicksPerSecond;
            public override long GetTimestamp() => _timestamp;
            public override ITimer CreateTimer(TimerCallback callback, object state,
                TimeSpan dueTime, TimeSpan period)
            {
                CreatedTimers++;
                Timer.Callback = callback;
                Timer.State = state;
                Timer.Change(dueTime, period);
                return Timer;
            }
            public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
        }

        private sealed class FakeTimer : ITimer
        {
            public TimerCallback Callback { get; set; }
            public object State { get; set; }
            public TimeSpan LastDueTime { get; private set; }
            public int Changes { get; private set; }
            public bool Disposed { get; private set; }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.That(dueTime == Timeout.InfiniteTimeSpan ||
                    dueTime >= TimeSpan.Zero && dueTime <= TimeSpan.FromMilliseconds(int.MaxValue), Is.True);
                Assert.That(period, Is.EqualTo(Timeout.InfiniteTimeSpan));
                LastDueTime = dueTime;
                Changes++;
                return !Disposed;
            }
            public void Fire() => Callback?.Invoke(State);
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
