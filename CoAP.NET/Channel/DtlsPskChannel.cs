/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 *
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
using System.Collections.Generic;
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
    /// CoAP channel over DTLS with pre-shared keys.
    /// </summary>
    public sealed class DtlsPskChannel : IChannel
    {
        private static readonly ILogger Log = CoapLogging.CreateLogger(typeof(DtlsPskChannel));
        private readonly IPEndPoint _localEndPoint;
        private readonly ConfiguredPskIdentityManager _identityManager;
        private readonly TimeSpan _sessionIdleTimeout;
        private readonly ConcurrentDictionary<IPEndPoint, DtlsPeerSession> _sessions
            = new ConcurrentDictionary<IPEndPoint, DtlsPeerSession>();
        private Socket _socket;
        private CancellationTokenSource _stop;
        private Task _receiveTask;
        private Task _cleanupTask;

        /// <summary>
        /// Initializes a DTLS PSK channel bound to the given UDP port.
        /// </summary>
        public DtlsPskChannel(Int32 port, IReadOnlyDictionary<String, String> pskKeys)
            : this(new IPEndPoint(IPAddress.Any, port), pskKeys, TimeSpan.FromMinutes(5))
        {
        }

        /// <summary>
        /// Initializes a DTLS PSK channel bound to the given UDP port.
        /// </summary>
        public DtlsPskChannel(
            Int32 port,
            IReadOnlyDictionary<String, String> pskKeys,
            TimeSpan sessionIdleTimeout)
            : this(new IPEndPoint(IPAddress.Any, port), pskKeys, sessionIdleTimeout)
        {
        }

        /// <summary>
        /// Initializes a DTLS PSK channel bound to the given local UDP endpoint.
        /// </summary>
        public DtlsPskChannel(IPEndPoint localEndPoint, IReadOnlyDictionary<String, String> pskKeys)
            : this(localEndPoint, pskKeys, TimeSpan.FromMinutes(5))
        {
        }

        /// <summary>
        /// Initializes a DTLS PSK channel bound to the given local UDP endpoint.
        /// </summary>
        public DtlsPskChannel(
            IPEndPoint localEndPoint,
            IReadOnlyDictionary<String, String> pskKeys,
            TimeSpan sessionIdleTimeout)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));
            if (localEndPoint.Port < 0 || localEndPoint.Port > UInt16.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(localEndPoint));
            if (pskKeys == null)
                throw new ArgumentNullException(nameof(pskKeys));
            if (pskKeys.Count == 0)
                throw new ArgumentException("At least one PSK identity must be configured.", nameof(pskKeys));

            _localEndPoint = localEndPoint;
            _identityManager = new ConfiguredPskIdentityManager(pskKeys);
            _sessionIdleTimeout = sessionIdleTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : sessionIdleTimeout;
        }

        /// <inheritdoc/>
        public System.Net.EndPoint LocalEndPoint
        {
            get { return _socket == null ? _localEndPoint : _socket.LocalEndPoint; }
        }

        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <inheritdoc/>
        public void Start()
        {
            if (_socket != null)
                return;

            _stop = new CancellationTokenSource();
            _socket = new Socket(_localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(_localEndPoint);
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

            foreach (DtlsPeerSession session in _sessions.Values)
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

            DtlsPeerSession session;
            if (_sessions.TryGetValue(remote, out session))
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
                        CreateReceiveAnyEndPoint(),
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

                Byte[] datagram = new Byte[received.ReceivedBytes];
                Buffer.BlockCopy(buffer, 0, datagram, 0, datagram.Length);
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("DTLS UDP datagram received from {RemoteEndPoint}: {ByteCount} bytes.", remote, datagram.Length);
                DtlsPeerSession session = _sessions.GetOrAdd(remote, CreateSession);
                session.Start();
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
                foreach (KeyValuePair<IPEndPoint, DtlsPeerSession> pair in _sessions)
                {
                    if (now - pair.Value.LastSeenUtc <= _sessionIdleTimeout)
                        continue;

                    DtlsPeerSession removed;
                    if (_sessions.TryRemove(pair.Key, out removed))
                        removed.Dispose();
                }
            }
        }

        private DtlsPeerSession CreateSession(IPEndPoint remote)
        {
            Socket socket = _socket;
            if (socket == null)
                throw new ObjectDisposedException(nameof(DtlsPskChannel));

            DtlsPeerSession session = new DtlsPeerSession(
                remote,
                socket,
                _identityManager,
                FireDataReceived,
                RemoveSession);
            if (Log.IsEnabled(LogLevel.Debug))
                Log.LogDebug("DTLS server session created for {RemoteEndPoint}.", remote);
            return session;
        }

        private IPEndPoint CreateReceiveAnyEndPoint()
        {
            IPAddress address = _localEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Any
                : IPAddress.Any;
            return new IPEndPoint(address, 0);
        }

        private void RemoveSession(IPEndPoint remote, DtlsPeerSession session)
        {
            DtlsPeerSession existing;
            if (_sessions.TryGetValue(remote, out existing) && Object.ReferenceEquals(existing, session))
            {
                DtlsPeerSession removed;
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

        private sealed class DtlsPeerSession : IDisposable
        {
            private readonly IPEndPoint _remote;
            private readonly DtlsSocketDatagramTransport _transport;
            private readonly ConfiguredPskIdentityManager _identityManager;
            private readonly Action<Byte[], IPEndPoint> _dataReceived;
            private readonly Action<IPEndPoint, DtlsPeerSession> _remove;
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();
            private readonly Object _sendLock = new Object();
            private Int32 _started;
            private Int32 _disposed;
            private DtlsTransport _dtls;

            public DtlsPeerSession(
                IPEndPoint remote,
                Socket socket,
                ConfiguredPskIdentityManager identityManager,
                Action<Byte[], IPEndPoint> dataReceived,
                Action<IPEndPoint, DtlsPeerSession> remove)
            {
                _remote = remote;
                _transport = new DtlsSocketDatagramTransport(socket, remote);
                _identityManager = identityManager;
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
                DtlsTransport dtls = _dtls;
                if (dtls == null)
                    return;

                try
                {
                    lock (_sendLock)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("DTLS server sending {ByteCount} bytes to {RemoteEndPoint}.", data.Length, _remote);
                        dtls.Send(data, 0, data.Length);
                    }
                }
                catch (IOException ex)
                {
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS send failed for {RemoteEndPoint}.", _remote);
                    _remove(_remote, this);
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                    return;
                _stop.Cancel();
                _transport.Close();
                try
                {
                    if (_dtls != null)
                        _dtls.Close();
                }
                catch (IOException)
                {
                }
                _stop.Dispose();
            }

            private void Run()
            {
                try
                {
                    BcTlsCrypto crypto = new BcTlsCrypto(new SecureRandom());
                    DtlsServerProtocol protocol = new DtlsServerProtocol();
                    PskTlsServer server = new DtlsPskTlsServer(crypto, _identityManager);
                    _dtls = protocol.Accept(server, _transport);
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("DTLS server handshake established for {RemoteEndPoint}.", _remote);

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
                            Log.LogDebug("DTLS server decrypted {ByteCount} bytes from {RemoteEndPoint}.", payload.Length, _remote);
                        _dataReceived(payload, _remote);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (TlsFatalAlert ex)
                {
                    if (!_stop.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS handshake failed for {RemoteEndPoint}.", _remote);
                }
                catch (IOException ex)
                {
                    if (!_stop.IsCancellationRequested && Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning(ex, "DTLS session ended for {RemoteEndPoint}.", _remote);
                }
                finally
                {
                    _remove(_remote, this);
                }
            }
        }

        private sealed class ConfiguredPskIdentityManager : TlsPskIdentityManager
        {
            private readonly Dictionary<String, Byte[]> _keys;

            public ConfiguredPskIdentityManager(IReadOnlyDictionary<String, String> keys)
            {
                _keys = new Dictionary<String, Byte[]>(keys.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<String, String> pair in keys)
                    _keys[pair.Key] = Encoding.UTF8.GetBytes(pair.Value);
            }

            public Byte[] GetHint()
            {
                return Array.Empty<Byte>();
            }

            public Byte[] GetPsk(Byte[] identity)
            {
                String name = Encoding.UTF8.GetString(identity);
                Byte[] key;
                return _keys.TryGetValue(name, out key) ? key : null;
            }
        }

        private sealed class DtlsPskTlsServer : PskTlsServer
        {
            public DtlsPskTlsServer(Org.BouncyCastle.Tls.Crypto.TlsCrypto crypto, TlsPskIdentityManager pskIdentityManager)
                : base(crypto, pskIdentityManager)
            {
            }

            protected override ProtocolVersion[] GetSupportedVersions()
            {
                return ProtocolVersion.DTLSv12.Only();
            }
        }
    }
}
