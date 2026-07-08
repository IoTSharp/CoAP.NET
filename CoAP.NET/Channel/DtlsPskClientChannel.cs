/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace CoAP.Channel
{
    /// <summary>
    /// CoAP client channel over DTLS with pre-shared keys.
    /// </summary>
    public sealed class DtlsPskClientChannel : IChannel
    {
        private static readonly ILogger Log = CoapLogging.CreateLogger(typeof(DtlsPskClientChannel));
        private readonly System.Net.EndPoint _configuredLocalEndPoint;
        private readonly Byte[] _identity;
        private readonly Byte[] _psk;
        private readonly TimeSpan _sessionIdleTimeout;
        private readonly ConcurrentDictionary<IPEndPoint, DtlsClientSession> _sessions
            = new ConcurrentDictionary<IPEndPoint, DtlsClientSession>();
        private Socket _socket;
        private CancellationTokenSource _stop;
        private Task _receiveTask;
        private Task _cleanupTask;

        /// <summary>
        /// Initializes a client DTLS PSK channel bound to an ephemeral UDP port.
        /// </summary>
        public DtlsPskClientChannel(String identity, String psk)
            : this(new IPEndPoint(IPAddress.Any, 0), identity, psk, TimeSpan.FromMinutes(5))
        {
        }

        /// <summary>
        /// Initializes a client DTLS PSK channel bound to the given local endpoint.
        /// </summary>
        public DtlsPskClientChannel(System.Net.EndPoint localEndPoint, String identity, String psk)
            : this(localEndPoint, identity, psk, TimeSpan.FromMinutes(5))
        {
        }

        /// <summary>
        /// Initializes a client DTLS PSK channel bound to the given local endpoint.
        /// </summary>
        public DtlsPskClientChannel(
            System.Net.EndPoint localEndPoint,
            String identity,
            String psk,
            TimeSpan sessionIdleTimeout)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));
            if (String.IsNullOrEmpty(identity))
                throw new ArgumentException("PSK identity must not be empty.", nameof(identity));
            if (String.IsNullOrEmpty(psk))
                throw new ArgumentException("PSK key must not be empty.", nameof(psk));

            _configuredLocalEndPoint = localEndPoint;
            _identity = Encoding.UTF8.GetBytes(identity);
            _psk = Encoding.UTF8.GetBytes(psk);
            _sessionIdleTimeout = sessionIdleTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : sessionIdleTimeout;
        }

        /// <inheritdoc/>
        public System.Net.EndPoint LocalEndPoint
        {
            get { return _socket == null ? _configuredLocalEndPoint : _socket.LocalEndPoint; }
        }

        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <inheritdoc/>
        public void Start()
        {
            if (_socket != null)
                return;

            _stop = new CancellationTokenSource();
            _socket = new Socket(_configuredLocalEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(_configuredLocalEndPoint);
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_stop.Token));
            _cleanupTask = Task.Run(() => CleanupLoopAsync(_stop.Token));
        }

        /// <inheritdoc/>
        public void Stop()
        {
            CancellationTokenSource stop = Interlocked.Exchange(ref _stop, null);
            if (stop != null)
                stop.Cancel();

            Socket socket = Interlocked.Exchange(ref _socket, null);
            if (socket != null)
            {
                try { socket.Dispose(); }
                catch (SocketException) { }
            }

            foreach (DtlsClientSession session in _sessions.Values)
                session.Dispose();
            _sessions.Clear();

            if (stop != null)
                stop.Dispose();
        }

        /// <inheritdoc/>
        public void Send(Byte[] data, System.Net.EndPoint ep)
        {
            IPEndPoint remote = ep as IPEndPoint;
            if (remote == null)
                return;

            DtlsClientSession session = _sessions.GetOrAdd(remote, CreateSession);
            session.Start();
            session.Send(data);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Stop();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            Byte[] buffer = new Byte[DtlsSocketDatagramTransport.DatagramLimit];
            while (!cancellationToken.IsCancellationRequested)
            {
                SocketReceiveFromResult received;
                try
                {
                    Socket socket = _socket;
                    if (socket == null)
                        return;
                    received = await socket.ReceiveFromAsync(
                        buffer,
                        SocketFlags.None,
                        new IPEndPoint(IPAddress.Any, 0),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    if (!cancellationToken.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS UDP receive failed.");
                    continue;
                }

                IPEndPoint remote = received.RemoteEndPoint as IPEndPoint;
                if (remote == null || received.ReceivedBytes <= 0)
                    continue;

                DtlsClientSession session;
                if (!_sessions.TryGetValue(remote, out session))
                    continue;

                Byte[] datagram = new Byte[received.ReceivedBytes];
                Buffer.BlockCopy(buffer, 0, datagram, 0, datagram.Length);
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("DTLS UDP datagram received from {RemoteEndPoint}: {ByteCount} bytes.", remote, datagram.Length);
                session.Enqueue(datagram);
            }
        }

        private async Task CleanupLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (var pair in _sessions)
                {
                    if (now - pair.Value.LastSeenUtc <= _sessionIdleTimeout)
                        continue;

                    DtlsClientSession removed;
                    if (_sessions.TryRemove(pair.Key, out removed))
                        removed.Dispose();
                }
            }
        }

        private DtlsClientSession CreateSession(IPEndPoint remote)
        {
            Socket socket = _socket;
            if (socket == null)
                throw new ObjectDisposedException(nameof(DtlsPskClientChannel));

            DtlsClientSession session = new DtlsClientSession(
                remote,
                socket,
                _identity,
                _psk,
                FireDataReceived,
                RemoveSession);
            if (Log.IsEnabled(LogLevel.Debug))
                Log.LogDebug("DTLS client session created for {RemoteEndPoint}.", remote);
            return session;
        }

        private void RemoveSession(IPEndPoint remote, DtlsClientSession session)
        {
            DtlsClientSession existing;
            if (_sessions.TryGetValue(remote, out existing) && Object.ReferenceEquals(existing, session))
            {
                DtlsClientSession removed;
                if (_sessions.TryRemove(remote, out removed))
                    removed.Dispose();
            }
        }

        private void FireDataReceived(Byte[] data, IPEndPoint remote)
        {
            EventHandler<DataReceivedEventArgs> handler = DataReceived;
            if (handler != null)
                handler(this, new DataReceivedEventArgs(data, remote));
        }

        private sealed class DtlsClientSession : IDisposable
        {
            private readonly IPEndPoint _remote;
            private readonly DtlsSocketDatagramTransport _transport;
            private readonly Byte[] _identity;
            private readonly Byte[] _psk;
            private readonly Action<Byte[], IPEndPoint> _dataReceived;
            private readonly Action<IPEndPoint, DtlsClientSession> _remove;
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();
            private readonly BlockingCollection<Byte[]> _outbound = new BlockingCollection<Byte[]>();
            private readonly Object _sendLock = new Object();
            private Int32 _started;
            private Int32 _disposed;
            private DtlsTransport _dtls;

            public DtlsClientSession(
                IPEndPoint remote,
                Socket socket,
                Byte[] identity,
                Byte[] psk,
                Action<Byte[], IPEndPoint> dataReceived,
                Action<IPEndPoint, DtlsClientSession> remove)
            {
                _remote = remote;
                _transport = new DtlsSocketDatagramTransport(socket, remote);
                _identity = identity;
                _psk = psk;
                _dataReceived = dataReceived;
                _remove = remove;
                LastSeenUtc = DateTimeOffset.UtcNow;
            }

            public DateTimeOffset LastSeenUtc { get; private set; }

            public void Start()
            {
                if (Interlocked.Exchange(ref _started, 1) == 1)
                    return;
                Task.Run((Action)Run);
            }

            public void Enqueue(Byte[] datagram)
            {
                LastSeenUtc = DateTimeOffset.UtcNow;
                _transport.Enqueue(datagram);
            }

            public void Send(Byte[] data)
            {
                LastSeenUtc = DateTimeOffset.UtcNow;
                try
                {
                    _outbound.Add(data);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                    return;
                _stop.Cancel();
                _transport.Close();
                _outbound.CompleteAdding();
                try
                {
                    if (_dtls != null)
                        _dtls.Close();
                }
                catch (IOException)
                {
                }
                _outbound.Dispose();
                _stop.Dispose();
            }

            private void Run()
            {
                try
                {
                    BcTlsCrypto crypto = new BcTlsCrypto(new SecureRandom());
                    DtlsClientProtocol protocol = new DtlsClientProtocol();
                    BasicTlsPskIdentity identity = new BasicTlsPskIdentity(_identity, _psk);
                    PskTlsClient client = new DtlsPskTlsClient(crypto, identity);
                    _dtls = protocol.Connect(client, _transport);
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("DTLS client handshake established for {RemoteEndPoint}.", _remote);

                    Task.Run(() => SendLoop(_dtls));

                    Byte[] buffer = new Byte[Math.Max(2048, _dtls.GetReceiveLimit())];
                    while (!_stop.IsCancellationRequested)
                    {
                        Int32 read = _dtls.ReceivePending(buffer, 0, buffer.Length, null);
                        if (read <= 0)
                            read = _dtls.Receive(buffer, 0, buffer.Length, 1000);
                        if (read <= 0)
                            continue;

                        LastSeenUtc = DateTimeOffset.UtcNow;
                        Byte[] payload = new Byte[read];
                        Buffer.BlockCopy(buffer, 0, payload, 0, read);
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("DTLS client decrypted {ByteCount} bytes from {RemoteEndPoint}.", payload.Length, _remote);
                        _dataReceived(payload, _remote);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (TlsFatalAlert ex)
                {
                    if (!_stop.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS client handshake failed for {RemoteEndPoint}.", _remote);
                }
                catch (IOException ex)
                {
                    if (!_stop.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS client session ended for {RemoteEndPoint}.", _remote);
                }
                finally
                {
                    _remove(_remote, this);
                }
            }

            private void SendLoop(DtlsTransport dtls)
            {
                try
                {
                    foreach (Byte[] data in _outbound.GetConsumingEnumerable(_stop.Token))
                    {
                        lock (_sendLock)
                        {
                            if (Log.IsEnabled(LogLevel.Debug))
                                Log.LogDebug("DTLS client sending {ByteCount} bytes to {RemoteEndPoint}.", data.Length, _remote);
                            dtls.Send(data, 0, data.Length);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException ex)
                {
                    if (!_stop.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS client send failed for {RemoteEndPoint}.", _remote);
                    _remove(_remote, this);
                }
            }
        }

        private sealed class DtlsPskTlsClient : PskTlsClient
        {
            public DtlsPskTlsClient(Org.BouncyCastle.Tls.Crypto.TlsCrypto crypto, TlsPskIdentity pskIdentity)
                : base(crypto, pskIdentity)
            {
            }

            protected override ProtocolVersion[] GetSupportedVersions()
            {
                return ProtocolVersion.DTLSv12.Only();
            }
        }
    }
}
