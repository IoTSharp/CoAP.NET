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

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Represents a request-level CoAP failure that can be converted to a stable response code.
    /// </summary>
    public class CoapRequestException : Exception
    {
        /// <summary>
        /// Creates a CoAP request exception.
        /// </summary>
        /// <param name="statusCode">The response code to send.</param>
        /// <param name="message">The diagnostic response message.</param>
        public CoapRequestException(StatusCode statusCode, string message)
            : this(statusCode, message, null)
        {
        }

        /// <summary>
        /// Creates a CoAP request exception.
        /// </summary>
        /// <param name="statusCode">The response code to send.</param>
        /// <param name="message">The diagnostic response message.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public CoapRequestException(
            StatusCode statusCode,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Gets the response code to send.
        /// </summary>
        public StatusCode StatusCode { get; }
    }

    /// <summary>
    /// Represents a malformed CoAP request.
    /// </summary>
    public sealed class CoapBadRequestException : CoapRequestException
    {
        /// <summary>
        /// Creates a malformed request exception.
        /// </summary>
        /// <param name="message">The diagnostic response message.</param>
        public CoapBadRequestException(string message)
            : base(StatusCode.BadRequest, message)
        {
        }

        /// <summary>
        /// Creates a malformed request exception.
        /// </summary>
        /// <param name="message">The diagnostic response message.</param>
        /// <param name="innerException">The exception that caused this failure.</param>
        public CoapBadRequestException(string message, Exception innerException)
            : base(StatusCode.BadRequest, message, innerException)
        {
        }
    }

    /// <summary>
    /// Represents an unsupported CoAP Content-Format or Accept value.
    /// </summary>
    public sealed class CoapUnsupportedMediaTypeException : CoapRequestException
    {
        /// <summary>
        /// Creates an unsupported media type exception.
        /// </summary>
        /// <param name="message">The diagnostic response message.</param>
        public CoapUnsupportedMediaTypeException(string message)
            : base(StatusCode.UnsupportedMediaType, message)
        {
        }
    }
}
