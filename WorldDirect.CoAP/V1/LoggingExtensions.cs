namespace WorldDirect.CoAP.V1
{
    using System;
    using Messages;
    using Microsoft.Extensions.Logging;

    internal static class LoggingExtensions
    {
        private static readonly Action<ILogger, CoapMessageId, CoapToken, int, Exception> SuccessfullyDeserializedMessageDelegate;

        static LoggingExtensions()
        {
            SuccessfullyDeserializedMessageDelegate = LoggerMessage.Define<CoapMessageId, CoapToken, int>(
                LogLevel.Debug,
                new EventId(0),
                "Deserialized message with ID {id}, token {token} and size of {bytes} bytes.");
        }

        internal static void SuccessfullyDeserializedMessage(this ILogger<CoapMessageSerializer> logger, CoapMessageId id, CoapToken token, int bytes)
            => SuccessfullyDeserializedMessageDelegate(logger, id, token, bytes, null);
    }
}