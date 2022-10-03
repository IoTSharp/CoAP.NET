namespace WorldDirect.CoAP
{
    using System;
    using System.Net;
    using Microsoft.Extensions.Logging;

    internal static class LoggingExtensions
    {
        private static readonly Action<ILogger, int, IPEndPoint, Exception> ReceivedMessageDelegate;

        static LoggingExtensions()
        {
            ReceivedMessageDelegate = LoggerMessage.Define<int, IPEndPoint>(
                LogLevel.Debug,
                new EventId(0),
                "Received {amountOfBytes} bytes from endpoint {remoteEndpoint}.");
        }

        internal static void ReceivedMessage(this ILogger<CoapServer> logger, int bytes, IPEndPoint remoteEndPoint)
            => ReceivedMessageDelegate(logger, bytes, remoteEndPoint, null);
    }
}