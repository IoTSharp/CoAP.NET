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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoAP.Channel;
using CoAP.Codec;
using Microsoft.Extensions.Logging;
using CoAP.Stack;
using CoAP.Threading;

namespace CoAP.Net
{
    /// <summary>
    /// EndPoint encapsulates the stack that executes the CoAP protocol.
    /// </summary>
    public partial class CoAPEndPoint : IEndPoint, IOutbox
    {
        static readonly ILogger Log = CoapLogging.CreateLogger(typeof(CoAPEndPoint));

        readonly ICoapConfig _config;
        readonly IChannel _channel;
        readonly CoapStack _coapStack;
        private IMessageDeliverer _deliverer;
        private IMatcher _matcher;
        private Int32 _running;
        private Int32 _disposed;
        private Int64 _lifecycleGeneration;
        private static readonly Object OwnedExchangeKey = new Object();
        private readonly Object _ownedSync = new Object();
        private readonly Dictionary<Exchange, OwnedExchange> _ownedExchanges = new Dictionary<Exchange, OwnedExchange>();
        private System.Net.EndPoint _localEP;
        private IExecutor _executor;

        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<Request>> SendingRequest;
        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<Response>> SendingResponse;
        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<EmptyMessage>> SendingEmptyMessage;
        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<Request>> ReceivingRequest;
        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<Response>> ReceivingResponse;
        /// <inheritdoc/>
        public event EventHandler<MessageEventArgs<EmptyMessage>> ReceivingEmptyMessage;

        /// <summary>
        /// Instantiates a new endpoint.
        /// </summary>
        public CoAPEndPoint()
            : this(0, CoapConfig.Default)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the specified configuration.
        /// </summary>
        public CoAPEndPoint(ICoapConfig config)
            : this(0, config)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the specified port.
        /// </summary>
        public CoAPEndPoint(Int32 port)
            : this(port, CoapConfig.Default)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the
        /// specified <see cref="System.Net.EndPoint"/>.
        /// </summary>
        public CoAPEndPoint(System.Net.EndPoint localEP)
            : this(localEP, CoapConfig.Default)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the
        /// specified port and configuration.
        /// </summary>
        public CoAPEndPoint(Int32 port, ICoapConfig config)
            : this(NewUDPChannel(port, config), config)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the
        /// specified <see cref="System.Net.EndPoint"/> and configuration.
        /// </summary>
        public CoAPEndPoint(System.Net.EndPoint localEP, ICoapConfig config)
            : this(NewUDPChannel(localEP, config), config)
        { }

        /// <summary>
        /// Instantiates a new endpoint with the
        /// specified channel and configuration.
        /// </summary>
        public CoAPEndPoint(IChannel channel, ICoapConfig config)
        {
            _config = config;
            _channel = channel;
            _matcher = new Matcher(config);
            _coapStack = new CoapStack(config);
            _channel.DataReceived += ReceiveData;
        }

        /// <inheritdoc/>
        public ICoapConfig Config
        {
            get { return _config; }
        }

        /// <summary>获取此端点实际支持的选项定义及自定义选项注册入口。</summary>
        public CoapOptionRegistry OptionRegistry { get; } = new CoapOptionRegistry();

        /// <summary>是否允许代理请求。仅在宿主提供实际代理消息分发器时启用；默认关闭。</summary>
        public Boolean SupportsProxying { get; set; }

        public IExecutor Executor
        {
            get { return _executor; }
            set
            {
                _executor = value ?? Executors.NoThreading;
                _coapStack.Executor = _executor;
            }
        }

        /// <inheritdoc/>
        public System.Net.EndPoint LocalEndPoint
        {
            get { return _localEP; }
        }

        /// <inheritdoc/>
        public IMessageDeliverer MessageDeliverer
        {
            set { _deliverer = value; }
            get
            {
                if (_deliverer == null)
                    _deliverer = new ClientMessageDeliverer();
                return _deliverer;
            }
        }

        /// <inheritdoc/>
        public IOutbox Outbox
        {
            get { return this; }
        }

        /// <inheritdoc/>
        public Boolean Running
        {
            get { return System.Threading.Volatile.Read(ref _running) > 0; }
        }

        /// <inheritdoc/>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) != 0, this);
            if (System.Threading.Interlocked.CompareExchange(ref _running, 1, 0) > 0)
                return;

            if (_executor == null)
                Executor = Executors.Default;

            _localEP = _channel.LocalEndPoint;
            try
            {
                _matcher.Start();
                _channel.Start();
                _localEP = _channel.LocalEndPoint;
            }
            catch
            {
                if (Log.IsEnabled(LogLevel.Warning))
                    Log.LogWarning("Cannot start endpoint at " + _localEP);
                Stop();
                throw;
            }
            if (Log.IsEnabled(LogLevel.Debug))
                Log.LogDebug("Starting endpoint bound to " + _localEP);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (System.Threading.Interlocked.Exchange(ref _running, 0) == 0)
                return;
            if (Log.IsEnabled(LogLevel.Debug))
                Log.LogDebug("Stopping endpoint bound to " + _localEP);
            _channel.Stop();
            _matcher.Stop();
            Clear();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            OwnedExchange[] retired;
            lock (_ownedSync)
            {
                System.Threading.Interlocked.Increment(ref _lifecycleGeneration);
                retired = _ownedExchanges.Values.ToArray();
                _ownedExchanges.Clear();
                foreach (OwnedExchange registration in retired)
                {
                    System.Threading.Volatile.Write(ref registration.Retired, 1);
                    DetachOwnedHandlers(registration);
                }
                _matcher.Clear();
            }
            // Reclaim every old protocol context before publishing callbacks;
            // callbacks may create replacement exchanges in the new generation.
            foreach (OwnedExchange registration in retired)
                ReleaseProtocolContexts(registration.Exchange);
            EventHandler notifications = null;
            foreach (OwnedExchange registration in retired)
            {
                Exchange exchange = registration.Exchange;
                Request current = exchange.CurrentRequest;
                notifications += (sender, args) =>
                {
                    if (!exchange.Complete)
                        exchange.Complete = true;
                };
                notifications += (sender, args) =>
                {
                    if (!registration.Request.IsCancelled &&
                        !registration.Request.IsTimedOut && !registration.Request.IsRejected)
                        registration.Request.Cancel();
                };
                notifications += (sender, args) =>
                {
                    if (current != null && !Object.ReferenceEquals(current, registration.Request) &&
                        !current.IsCancelled && !current.IsTimedOut && !current.IsRejected)
                        current.Cancel();
                };
            }
            Message.DispatchTerminalEvent(notifications, this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            try
            {
                Stop();
            }
            finally
            {
                try
                {
                    (_coapStack.Get("Blockwise") as IDisposable)?.Dispose();
                }
                finally
                {
                    try { _channel.Dispose(); }
                    finally { (_matcher as IDisposable)?.Dispose(); }
                }
            }
        }

        /// <inheritdoc/>
        public void SendRequest(Request request)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (!IsCurrentGeneration(generation))
            {
                request.Cancel();
                return;
            }
            Exchange exchange = new Exchange(request, Origin.Local) { EndPoint = this, Request = request };
            if (!TryOwnExchange(exchange, generation))
            {
                request.Cancel();
                return;
            }
            _executor.Start(() =>
            {
                if (CanUseExchange(exchange))
                    _coapStack.SendRequest(exchange, request);
                else
                    request.Cancel();
            });
        }

        /// <inheritdoc/>
        public void SendResponse(Exchange exchange, Response response)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (!TryOwnExchange(exchange, generation))
            {
                response.Cancel();
                return;
            }
            _executor.Start(() =>
            {
                if (CanUseExchange(exchange))
                    _coapStack.SendResponse(exchange, response);
                else
                    response.Cancel();
            });
        }

        /// <inheritdoc/>
        public void SendEmptyMessage(Exchange exchange, EmptyMessage message)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (exchange != null && !TryOwnExchange(exchange, generation) || !IsCurrentGeneration(generation))
            {
                message.Cancel();
                return;
            }
            _executor.Start(() =>
            {
                if (IsCurrentGeneration(generation) && (exchange == null || CanUseExchange(exchange)))
                    _coapStack.SendEmptyMessage(exchange, message);
                else
                    message.Cancel();
            });
        }

        private void ReceiveData(Object sender, DataReceivedEventArgs e)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (IsCurrentGeneration(generation))
                _executor.Start(() => ReceiveData(e, generation));
        }

        private Boolean IsCurrentGeneration(Int64 generation)
        {
            return Running && System.Threading.Volatile.Read(ref _disposed) == 0
                && generation == System.Threading.Volatile.Read(ref _lifecycleGeneration);
        }

        private void ReceiveData(DataReceivedEventArgs e, Int64 generation)
        {
            if (!IsCurrentGeneration(generation))
                return;
            IMessageDecoder decoder;
            try
            {
                decoder = Spec.NewMessageDecoder(e.Data);
            }
            catch (Exception ex) when (IsMessageFormatException(ex))
            {
                // A datagram without a complete fixed header does not provide
                // enough information to construct a safe reply. Drop it.
                if (Log.IsEnabled(LogLevel.Warning))
                    Log.LogWarning("Ignoring malformed CoAP datagram from " + e.EndPoint);
                return;
            }

            if (!decoder.IsWellFormed)
            {
                if (Log.IsEnabled(LogLevel.Warning))
                    Log.LogWarning("Ignoring malformed CoAP header from " + e.EndPoint);
                HandleMalformedDatagram(decoder, e);
                return;
            }

            if (decoder.IsRequest)
            {
                Request request;
                try
                {
                    request = decoder.DecodeRequest();
                }
                catch (Exception ex) when (IsMessageFormatException(ex))
                {
                    HandleMalformedDatagram(decoder, e);
                    return;
                }

                request.Source = e.EndPoint;

                if (!OptionRegistry.ValidateAndFilter(request, out String diagnostic))
                {
                    if (IsCurrentGeneration(generation) && request.Type == MessageType.CON)
                        SendOptionError(request, StatusCode.BadOption, diagnostic);
                    return;
                }
                if (!IsCurrentGeneration(generation))
                    return;
                if (!SupportsProxying && (request.HasOption(OptionType.ProxyUri) ||
                    request.HasOption(OptionType.ProxyScheme)))
                {
                    SendOptionError(request, StatusCode.ProxyingNotSupported, null);
                    return;
                }

                Fire(ReceivingRequest, request);

                if (IsCurrentGeneration(generation) && !request.IsCancelled)
                {
                    Exchange exchange = _matcher.ReceiveRequest(request);
                    if (exchange != null)
                    {
                        if (!TryOwnExchange(exchange, generation))
                            return;
                        exchange.EndPoint = this;
                        _coapStack.ReceiveRequest(exchange, request);
                    }
                }
            }
            else if (decoder.IsResponse)
            {
                Response response;
                try
                {
                    response = decoder.DecodeResponse();
                }
                catch (Exception ex) when (IsMessageFormatException(ex))
                {
                    // Reject a malformed CON with RST, while ACK/RST and NON
                    // are silently discarded without reaching the matcher.
                    HandleMalformedDatagram(decoder, e);
                    return;
                }
                response.Source = e.EndPoint;

                if (!OptionRegistry.ValidateAndFilter(response, out _))
                {
                    if (IsCurrentGeneration(generation) && response.Type == MessageType.CON)
                        Reject(response);
                    return;
                }
                if (!IsCurrentGeneration(generation))
                    return;

                Fire(ReceivingResponse, response);

                if (IsCurrentGeneration(generation) && !response.IsCancelled)
                {
                    Exchange exchange = _matcher.ReceiveResponse(response);
                    if (exchange != null)
                    {
                        if (!CanUseExchange(exchange))
                            return;
                        response.RTT = (DateTime.Now - exchange.Timestamp).TotalMilliseconds;
                        exchange.EndPoint = this;
                        _coapStack.ReceiveResponse(exchange, response);
                    }
                    else if (response.Type != MessageType.ACK)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Rejecting unmatchable response from " + e.EndPoint);
                        Reject(response);
                    }
                }
            }
            else if (decoder.IsEmpty)
            {
                EmptyMessage message;
                try
                {
                    message = decoder.DecodeEmptyMessage();
                }
                catch (Exception ex) when (IsMessageFormatException(ex))
                {
                    // Empty messages must contain no token, options, or
                    // payload. Apply the same message-format error handling.
                    HandleMalformedDatagram(decoder, e);
                    return;
                }
                message.Source = e.EndPoint;

                Fire(ReceivingEmptyMessage, message);

                if (IsCurrentGeneration(generation) && !message.IsCancelled)
                {
                    // CoAP Ping
                    if (message.Type == MessageType.CON || message.Type == MessageType.NON)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Responding to ping by " + e.EndPoint);
                        Reject(message);
                    }
                    else
                    {
                        Exchange exchange = _matcher.ReceiveEmptyMessage(message);
                        if (exchange != null)
                        {
                            if (!CanUseExchange(exchange))
                                return;
                            exchange.EndPoint = this;
                            _coapStack.ReceiveEmptyMessage(exchange, message);
                        }
                    }
                }
            }
            else if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Silently ignoring non-CoAP message from " + e.EndPoint);
            }
        }

        private void SendOptionError(Request request, StatusCode status, String diagnostic)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            Response response = Response.CreateResponse(request, status);
            response.Type = request.Type == MessageType.CON ? MessageType.ACK : MessageType.NON;
            response.ID = request.Type == MessageType.CON ? request.ID : Message.None;
            if (diagnostic != null)
                response.Payload = System.Text.Encoding.UTF8.GetBytes(diagnostic);
            if (request.Type == MessageType.NON)
            {
                // A NON reply uses the local MID namespace and the normal
                // exchange cleanup instead of echoing the request's MID.
                Exchange exchange = _matcher.ReceiveRequest(request);
                if (!TryOwnExchange(exchange, generation))
                    return;
                exchange.EndPoint = this;
                exchange.Request = request;
                _coapStack.SendResponse(exchange, response);
                return;
            }
            Fire(SendingResponse, response);
            if (IsCurrentGeneration(generation) && !response.IsCancelled)
                _channel.Send(Serialize(response), response.Destination);
        }

        private void HandleMalformedDatagram(IMessageDecoder decoder, DataReceivedEventArgs e)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (Log.IsEnabled(LogLevel.Warning))
                Log.LogWarning("Message format error caused by " + e.EndPoint);

            // RFC 7252 section 4.2 requires a reset for a malformed
            // Confirmable message when its version, type, and message ID are
            // known. Never reflect a reset to NON, ACK, RST, or an invalid
            // fixed header.
            if (e.Data == null || e.Data.Length < 4 || decoder.Version != 1 ||
                ((e.Data[0] >> 4) & 0x03) != (Int32)MessageType.CON)
                return;

            EmptyMessage rst = new EmptyMessage(MessageType.RST)
            {
                Destination = e.EndPoint,
                ID = decoder.ID
            };

            Fire(SendingEmptyMessage, rst);
            if (IsCurrentGeneration(generation) && !rst.IsCancelled)
                _channel.Send(Serialize(rst), rst.Destination);
        }

        private static Boolean IsMessageFormatException(Exception exception)
        {
            return exception is ArgumentException || exception is FormatException ||
                exception is InvalidOperationException || exception is EndOfStreamException;
        }

        private void Reject(Message message)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            EmptyMessage rst = EmptyMessage.NewRST(message);

            Fire(SendingEmptyMessage, rst);

            if (IsCurrentGeneration(generation) && !rst.IsCancelled)
                _channel.Send(Serialize(rst), rst.Destination);
        }

        private Byte[] Serialize(EmptyMessage message)
        {
            Byte[] bytes = message.Bytes;
            if (bytes == null)
            {
                bytes = Spec.NewMessageEncoder().Encode(message);
                message.Bytes = bytes;
            }
            return bytes;
        }

        private Byte[] Serialize(Request request)
        {
            Byte[] bytes = request.Bytes;
            if (bytes == null)
            {
                bytes = Spec.NewMessageEncoder().Encode(request);
                request.Bytes = bytes;
            }
            return bytes;
        }

        private Byte[] Serialize(Response response)
        {
            Byte[] bytes = response.Bytes;
            if (bytes == null)
            {
                bytes = Spec.NewMessageEncoder().Encode(response);
                response.Bytes = bytes;
            }
            return bytes;
        }

        private void Fire<T>(EventHandler<MessageEventArgs<T>> handler, T msg) where T : Message
        {
            if (handler != null)
                handler(this, new MessageEventArgs<T>(msg));
        }

        static IChannel NewUDPChannel(Int32 port, ICoapConfig config)
        {
            UDPChannel channel = new UDPChannel(port);
            channel.ReceiveBufferSize = config.ChannelReceiveBufferSize;
            channel.SendBufferSize = config.ChannelSendBufferSize;
            channel.ReceivePacketSize = config.ChannelReceivePacketSize;
            return channel;
        }

        static IChannel NewUDPChannel(System.Net.EndPoint localEP, ICoapConfig config)
        {
            UDPChannel channel = new UDPChannel(localEP);
            channel.ReceiveBufferSize = config.ChannelReceiveBufferSize;
            channel.SendBufferSize = config.ChannelSendBufferSize;
            channel.ReceivePacketSize = config.ChannelReceivePacketSize;
            return channel;
        }

        void IOutbox.SendRequest(Exchange exchange, Request request)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (!TryOwnExchange(exchange, generation))
            {
                request.Cancel();
                return;
            }
            Boolean allowed;
            lock (exchange)
            {
                allowed = CanUseExchange(exchange) && CanSendRequest(exchange, request);
                if (allowed)
                    _matcher.SendRequest(exchange, request);
            }
            if (!allowed)
            {
                if (!request.IsCancelled && !request.IsTimedOut && !request.IsRejected)
                    request.Cancel();
                return;
            }

            Fire(SendingRequest, request);

            if (CanUseExchange(exchange) && CanSendRequest(exchange, request))
                _channel.Send(Serialize(request), request.Destination);
            else if (!request.IsCancelled && !request.IsTimedOut && !request.IsRejected)
                request.Cancel();
        }

        private Boolean CanSendRequest(Exchange exchange, Request request)
        {
            Request original = exchange.Request ?? request;
            return Running && !exchange.Complete
                && !BlockwiseLayer.IsExchangeTerminating(exchange)
                && !request.IsCancelled && !request.IsTimedOut && !request.IsRejected
                && !original.IsCancelled && !original.IsTimedOut && !original.IsRejected;
        }

        void IOutbox.SendResponse(Exchange exchange, Response response)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (!TryOwnExchange(exchange, generation))
            {
                response.Cancel();
                return;
            }
            _matcher.SendResponse(exchange, response);

            Fire(SendingResponse, response);

            if (CanUseExchange(exchange) && !response.IsCancelled)
                _channel.Send(Serialize(response), response.Destination);
        }

        void IOutbox.SendEmptyMessage(Exchange exchange, EmptyMessage message)
        {
            Int64 generation = System.Threading.Volatile.Read(ref _lifecycleGeneration);
            if (!IsCurrentGeneration(generation) || exchange != null && !TryOwnExchange(exchange, generation))
            {
                message.Cancel();
                return;
            }
            _matcher.SendEmptyMessage(exchange, message);

            Fire(SendingEmptyMessage, message);

            if (IsCurrentGeneration(generation) && (exchange == null || CanUseExchange(exchange)) && !message.IsCancelled)
                _channel.Send(Serialize(message), message.Destination);
        }

        internal static Boolean IsExchangeRetired(Exchange exchange)
        {
            OwnedExchange registration = exchange.Get(OwnedExchangeKey) as OwnedExchange;
            return registration != null && (System.Threading.Volatile.Read(ref registration.Retired) != 0 ||
                !registration.Owner.IsCurrentGeneration(registration.Generation));
        }

        private Boolean CanUseExchange(Exchange exchange)
        {
            OwnedExchange registration = exchange.Get(OwnedExchangeKey) as OwnedExchange;
            return registration != null && Object.ReferenceEquals(registration.Owner, this) &&
                !IsExchangeRetired(exchange);
        }

        private Boolean TryOwnExchange(Exchange exchange, Int64 generation)
        {
            lock (_ownedSync)
            {
                if (!IsCurrentGeneration(generation))
                    return false;
                OwnedExchange existing = exchange.Get(OwnedExchangeKey) as OwnedExchange;
                if (existing != null)
                    return Object.ReferenceEquals(existing.Owner, this) && !IsExchangeRetired(exchange);
                Request original = exchange.Request ?? exchange.CurrentRequest;
                OwnedExchange registration = new OwnedExchange(this, exchange, original, generation);
                existing = exchange.GetOrAdd(OwnedExchangeKey, registration) as OwnedExchange;
                if (!Object.ReferenceEquals(existing, registration))
                    return Object.ReferenceEquals(existing.Owner, this) && !IsExchangeRetired(exchange);
                registration.Completed = (sender, args) => FinishOwnedExchange(registration, false);
                registration.Terminated = (sender, args) => FinishOwnedExchange(registration, true);
                exchange.Completed += registration.Completed;
                original.Cancelled += registration.Terminated;
                original.TimedOut += registration.Terminated;
                original.Rejected += registration.Terminated;
                _ownedExchanges[exchange] = registration;
                return true;
            }
        }

        private void FinishOwnedExchange(OwnedExchange registration, Boolean terminate)
        {
            lock (_ownedSync)
            {
                _ownedExchanges.Remove(registration.Exchange);
                DetachOwnedHandlers(registration);
                if (terminate)
                    System.Threading.Volatile.Write(ref registration.Retired, 1);
            }
            ReleaseProtocolContexts(registration.Exchange);
            if (terminate && !registration.Exchange.Complete)
                registration.Exchange.Complete = true;
        }

        private static void DetachOwnedHandlers(OwnedExchange registration)
        {
            registration.Exchange.Completed -= registration.Completed;
            registration.Request.Cancelled -= registration.Terminated;
            registration.Request.TimedOut -= registration.Terminated;
            registration.Request.Rejected -= registration.Terminated;
        }

        private void ReleaseProtocolContexts(Exchange exchange)
        {
            (_coapStack.Get("Blockwise") as BlockwiseLayer)?.ReleaseExchange(exchange);
            ObserveLayer.ReleaseExchange(exchange);
            ReliabilityLayer.ReleaseExchange(exchange);
        }

        private sealed class OwnedExchange
        {
            internal OwnedExchange(CoAPEndPoint owner, Exchange exchange, Request request, Int64 generation)
            { Owner = owner; Exchange = exchange; Request = request; Generation = generation; }
            internal CoAPEndPoint Owner { get; }
            internal Exchange Exchange { get; }
            internal Request Request { get; }
            internal Int64 Generation { get; }
            internal Int32 Retired;
            internal EventHandler Completed { get; set; }
            internal EventHandler Terminated { get; set; }
        }
    }
}
