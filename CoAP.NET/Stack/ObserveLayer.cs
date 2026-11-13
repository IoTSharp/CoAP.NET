/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 * 
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Threading;
using System.Timers;
using Microsoft.Extensions.Logging;
using CoAP.Net;
using CoAP.Observe;

namespace CoAP.Stack
{
    public class ObserveLayer : AbstractLayer
    {
        static readonly ILogger Log = CoapLogging.CreateLogger(typeof(ObserveLayer));
        static readonly Object ReregistrationContextKey = "ReregistrationContext";

        /// <summary>
        /// Additional time to wait until re-registration
        /// </summary>
        private readonly Int32 _backoff;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Constructs a new observe layer.
        /// </summary>
        public ObserveLayer(ICoapConfig config) : this(config, TimeProvider.System)
        {
        }

        /// <summary>
        /// Constructs an observe layer using the supplied monotonic clock and timer provider.
        /// </summary>
        public ObserveLayer(ICoapConfig config, TimeProvider timeProvider)
        {
            _backoff = config.NotificationReregistrationBackoff;
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc/>
        public override void SendResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            ObserveRelation relation = exchange.Relation;
            if (relation != null && relation.Established)
            {
                if (exchange.Request.IsAcknowledged || exchange.Request.Type == MessageType.NON)
                {
                    // Transmit errors as CON
                    if (!Code.IsSuccess(response.Code))
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Response has error code " + response.Code + " and must be sent as CON");
                        response.Type = MessageType.CON;
                        relation.Cancel();
                    }
                    else
                    {
                        // Make sure that every now and than a CON is mixed within
                        if (relation.Check())
                        {
                            if (Log.IsEnabled(LogLevel.Debug))
                                Log.LogDebug("The observe relation check requires the notification to be sent as CON");
                            response.Type = MessageType.CON;
                        }
                        else
                        {
                            // By default use NON, but do not override resource decision
                            if (response.Type == MessageType.Unknown)
                                response.Type = MessageType.NON;
                        }
                    }
                }

                // This is a notification
                response.Last = false;

                /*
                 * The matcher must be able to find the NON notifications to remove
                 * them from the exchangesByID map
                 */
                if (response.Type == MessageType.NON)
                {
                    relation.AddNotification(response);
                }

                /*
                 * Only one Confirmable message is allowed to be in transit. A CON
                 * is in transit as long as it has not been acknowledged, rejected,
                 * or timed out. All further notifications are postponed here. If a
                 * former CON is acknowledged or timeouts, it starts the freshest
                 * notification (In case of a timeout, it keeps the retransmission
                 * counter). When a fresh/younger notification arrives but must be
                 * postponed we forget any former notification.
                 */
                if (response.Type == MessageType.CON)
                {
                    PrepareSelfReplacement(nextLayer, exchange, response);
                }

                // The decision whether to postpone this notification or not and the
                // decision which notification is the freshest to send next must be
                // synchronized
                lock (exchange)
                {
                    Response current = relation.CurrentControlNotification;
                    if (current != null && IsInTransit(current))
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("A former notification is still in transit. Postpone " + response);
                        // use the same ID
                        response.ID = current.ID;
                        relation.NextControlNotification = response;
                        return;
                    }
                    else
                    {
                        relation.CurrentControlNotification = response;
                        relation.NextControlNotification = null;
                    }
                }
            }
            // else no observe was requested or the resource does not allow it
            base.SendResponse(nextLayer, exchange, response);
        }

        /// <inheritdoc/>
        public override void ReceiveResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            if (CoAPEndPoint.IsExchangeRetired(exchange))
                return;
            if (response.HasOption(OptionType.Observe))
            {
                if (exchange.Request.IsCancelled)
                {
                    // The request was canceled and we no longer want notifications
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("ObserveLayer rejecting notification for canceled Exchange");
                    EmptyMessage rst = EmptyMessage.NewRST(response);
                    SendEmptyMessage(nextLayer, exchange, rst);
                    // Matcher sets exchange as complete when RST is sent
                }
                else
                {
                    PrepareReregistration(exchange, response, msg => SendRequest(nextLayer, exchange, msg));
                    base.ReceiveResponse(nextLayer, exchange, response);
                }
            }
            else
            {
                // No observe option in response => always deliver
                ReleaseExchange(exchange);
                base.ReceiveResponse(nextLayer, exchange, response);
            }
        }

        /// <inheritdoc/>
        public override void ReceiveEmptyMessage(INextLayer nextLayer, Exchange exchange, EmptyMessage message)
        {
            // NOTE: We could also move this into the MessageObserverAdapter from
            // sendResponse into the method rejected().
            if (message.Type == MessageType.RST && exchange.Origin == Origin.Remote)
            {
                // The response has been rejected
                ObserveRelation relation = exchange.Relation;
                if (relation != null)
                {
                    relation.Cancel();
                } // else there was no observe relation ship and this layer ignores the rst
            }
            base.ReceiveEmptyMessage(nextLayer, exchange, message);
        }

        private static Boolean IsInTransit(Response response)
        {
            MessageType type = response.Type;
            Boolean acked = response.IsAcknowledged;
            Boolean timeout = response.IsTimedOut;
            Boolean result = type == MessageType.CON && !acked && !timeout;
            return result;
        }

        private void PrepareSelfReplacement(INextLayer nextLayer, Exchange exchange, Response response)
        {
            response.Acknowledged += (o, e) =>
            {
                lock (exchange)
                {
                    ObserveRelation relation = exchange.Relation;
                    Response next = relation.NextControlNotification;
                    relation.CurrentControlNotification = next; // next may be null
                    relation.NextControlNotification = null;
                    if (next != null)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Notification has been acknowledged, send the next one");
                        // this is not a self replacement, hence a new ID
                        next.ID = Message.None;
                        // Create a new task for sending next response so that we can leave the sync-block
                        Executor.Start(() => SendResponse(nextLayer, exchange, next));
                    }
                }
            };

            response.Retransmitting += (o, e) =>
            {
                lock (exchange)
                {
                    ObserveRelation relation = exchange.Relation;
                    Response next = relation.NextControlNotification;
                    if (next != null)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("The notification has timed out and there is a fresher notification for the retransmission.");
                        // Cancel the original retransmission and send the fresh notification here
                        response.IsCancelled = true;
                        // use the same ID
                        next.ID = response.ID;
                        // Convert all notification retransmissions to CON
                        if (next.Type != MessageType.CON)
                        {
                            next.Type = MessageType.CON;
                            PrepareSelfReplacement(nextLayer, exchange, next);
                        }
                        relation.CurrentControlNotification = next;
                        relation.NextControlNotification = null;
                        // Create a new task for sending next response so that we can leave the sync-block
                        Executor.Start(() => SendResponse(nextLayer, exchange, next));
                    }
                }
            };

            response.TimedOut += (o, e) =>
            {
                ObserveRelation relation = exchange.Relation;
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Notification" + relation.Exchange.Request.TokenString
                        + " timed out. Cancel all relations with source " + relation.Source);
                relation.CancelAll();
            };
        }

        private void PrepareReregistration(Exchange exchange, Response response, Action<Request> reregister)
        {
            lock (exchange)
            {
                if (CoAPEndPoint.IsExchangeRetired(exchange) || exchange.Complete ||
                    exchange.Request.IsCancelled || exchange.Request.IsTimedOut || exchange.Request.IsRejected)
                    return;
                Int64 timeout = Math.Max(0, response.MaxAge * 1000 + _backoff);
                ReregistrationContext ctx = exchange.GetOrAdd<ReregistrationContext>(
                    ReregistrationContextKey, _ => new ReregistrationContext(exchange, _timeProvider, reregister));

                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Scheduling re-registration in " + timeout + "ms for " + exchange.Request);

                ctx.Restart(timeout);
            }
        }

        internal static void ReleaseExchange(Exchange exchange)
        {
            ReregistrationContext context;
            lock (exchange)
                context = exchange.Remove(ReregistrationContextKey) as ReregistrationContext;
            context?.Dispose();
        }

        class ReregistrationContext : IDisposable
        {
            private readonly Exchange _exchange;
            private readonly Request _request;
            private readonly Action<Request> _reregister;
            private readonly TimeProvider _timeProvider;
            private ITimer _timer;
            private Int64 _started;
            private TimeSpan _timeout;
            private Int64 _generation;
            private Boolean _armed;
            private Boolean _disposed;

            public ReregistrationContext(Exchange exchange, TimeProvider timeProvider, Action<Request> reregister)
            {
                _exchange = exchange;
                _request = exchange.Request;
                _reregister = reregister;
                _timeProvider = timeProvider;
                _timer = timeProvider.CreateTimer(OnTimer, null,
                    Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _request.Cancelled += Terminated;
                _request.TimedOut += Terminated;
                _request.Rejected += Terminated;
                exchange.Completed += Terminated;
            }

            public void Restart(Int64 timeout)
            {
                lock (_exchange)
                {
                    if (_disposed)
                        return;
                    _started = _timeProvider.GetTimestamp();
                    _timeout = TimeSpan.FromMilliseconds(timeout);
                    _generation++;
                    _armed = true;
                    Schedule(_timeout);
                }
            }

            private void Schedule(TimeSpan remaining)
            {
                // A legal UInt32 Max-Age can exceed the platform timer range by
                // centuries. Keep one timer and recheck the monotonic deadline
                // after each bounded slice, without shortening the total wait.
                TimeSpan slice = TimeSpan.FromMilliseconds(Int32.MaxValue);
                _timer.Change(remaining < slice ? remaining : slice, Timeout.InfiniteTimeSpan);
            }

            private void Terminated(Object sender, EventArgs args) => Dispose();

            private void OnTimer(Object state)
            {
                try
                {
                    timer_Elapsed(null, null);
                }
                catch (Exception error)
                {
                    // Thread-pool timer callbacks have no synchronous caller.
                    // Report user/lower-layer failures without terminating it.
                    Dispose();
                    Log.LogError(error, "Observe re-registration failed");
                }
            }

            public void Dispose()
            {
                ITimer timer;
                lock (_exchange)
                {
                    if (_disposed)
                        return;
                    _disposed = true;
                    _armed = false;
                    _generation++;
                    timer = _timer;
                    _timer = null;
                    _request.Cancelled -= Terminated;
                    _request.TimedOut -= Terminated;
                    _request.Rejected -= Terminated;
                    _exchange.Completed -= Terminated;
                    if (ReferenceEquals(_exchange.Get(ReregistrationContextKey), this))
                        _exchange.Remove(ReregistrationContextKey);
                }
                timer?.Dispose();
            }

            void timer_Elapsed(Object sender, ElapsedEventArgs e)
            {
                Request request;
                Int64 generation;
                lock (_exchange)
                {
                    if (_disposed || !_armed)
                        return;
                    request = _exchange.Request;
                    if (IsTerminated(request))
                    {
                        Dispose();
                        return;
                    }
                    TimeSpan remaining = _timeout - _timeProvider.GetElapsedTime(_started);
                    if (remaining > TimeSpan.Zero)
                    {
                        Schedule(remaining);
                        return;
                    }
                    _armed = false;
                    generation = _generation;
                }

                Request refresh = Request.NewGet();
                refresh.SetOptions(request.GetOptions());
                refresh.MarkObserve();
                refresh.Token = request.Token;
                refresh.Destination = request.Destination;
                refresh.CopyEventHandler(request);
                // The context owns the original subscription. Do not duplicate
                // its cleanup handlers onto each refresh request.
                refresh.Cancelled -= Terminated;
                refresh.TimedOut -= Terminated;
                refresh.Rejected -= Terminated;
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Re-registering for " + request);
                try
                {
                    request.FireReregister(refresh);
                }
                catch
                {
                    Dispose();
                    throw;
                }
                lock (_exchange)
                {
                    if (_disposed || generation != _generation || IsTerminated(request))
                        return;
                }
                try
                {
                    _reregister(refresh);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private Boolean IsTerminated(Request request)
            {
                return CoAPEndPoint.IsExchangeRetired(_exchange) || _exchange.Complete ||
                    request.IsCancelled || request.IsTimedOut || request.IsRejected;
            }
        }
    }
}
