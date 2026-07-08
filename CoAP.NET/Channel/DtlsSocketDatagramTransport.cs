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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Org.BouncyCastle.Tls;

namespace CoAP.Channel
{
    /// <summary>
    /// Adapts a shared UDP socket plus a peer-specific queue to BouncyCastle DTLS.
    /// </summary>
    internal sealed class DtlsSocketDatagramTransport : DatagramTransport
    {
        public const Int32 DatagramLimit = 65507;

        private readonly BlockingCollection<Byte[]> _inbound = new BlockingCollection<Byte[]>();
        private readonly Socket _socket;
        private readonly IPEndPoint _remote;
        private Int32 _closed;

        public DtlsSocketDatagramTransport(Socket socket, IPEndPoint remote)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        public Int32 GetReceiveLimit()
        {
            return DatagramLimit;
        }

        public Int32 GetSendLimit()
        {
            return DatagramLimit;
        }

        public Int32 Receive(Byte[] buf, Int32 off, Int32 len, Int32 waitMillis)
        {
            Byte[] datagram;
            if (!TryTake(waitMillis, out datagram))
                return -1;

            Int32 count = Math.Min(len, datagram.Length);
            Buffer.BlockCopy(datagram, 0, buf, off, count);
            return count;
        }

        public Int32 Receive(Span<Byte> buffer, Int32 waitMillis)
        {
            Byte[] datagram;
            if (!TryTake(waitMillis, out datagram))
                return -1;

            Int32 count = Math.Min(buffer.Length, datagram.Length);
            datagram.AsSpan(0, count).CopyTo(buffer);
            return count;
        }

        public void Send(Byte[] buf, Int32 off, Int32 len)
        {
            if (_closed != 0)
                return;
            _socket.SendTo(buf.AsSpan(off, len), SocketFlags.None, _remote);
        }

        public void Send(ReadOnlySpan<Byte> buffer)
        {
            if (_closed != 0)
                return;
            _socket.SendTo(buffer, SocketFlags.None, _remote);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1)
                return;
            _inbound.CompleteAdding();
            _inbound.Dispose();
        }

        public void Enqueue(Byte[] datagram)
        {
            if (_closed != 0)
                return;

            try
            {
                _inbound.Add(datagram);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private Boolean TryTake(Int32 waitMillis, out Byte[] datagram)
        {
            datagram = null;
            if (_closed != 0)
                return false;

            try
            {
                return _inbound.TryTake(out datagram, waitMillis < 0 ? Timeout.Infinite : waitMillis);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
