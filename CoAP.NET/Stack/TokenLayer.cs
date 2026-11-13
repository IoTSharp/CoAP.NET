/*
 * Copyright (c) 2011-2014, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 * 
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Security.Cryptography;
using CoAP.Net;

namespace CoAP.Stack
{
    /// <summary>
    /// Assigns cryptographically random eight-byte tokens when the caller has
    /// not supplied one. Empty tokens are byte arrays of length zero, not null.
    /// Transport security context binding is not provided by this layer.
    /// </summary>
    public class TokenLayer : AbstractLayer
    {
        /// <summary>
        /// Constructs a new token layer.
        /// </summary>
        public TokenLayer(ICoapConfig config)
        {
            // Keep the constructor and configuration surface compatible. The old
            // UseRandomTokenStart setting must not disable fresh randomness.
        }

        /// <inheritdoc/>
        public override void SendRequest(INextLayer nextLayer, Exchange exchange, Request request)
        {
            if (request.Token == null)
                request.Token = NewToken();
            base.SendRequest(nextLayer, exchange, request);
        }

        /// <inheritdoc/>
        public override void SendResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            // A response must have the same token as the request it belongs to. If
            // the token is empty, we must use a byte array of length 0.
            if (response.Token == null)
                response.Token = exchange.CurrentRequest.Token;
            base.SendResponse(nextLayer, exchange, response);
        }

        /// <inheritdoc/>
        public override void ReceiveRequest(INextLayer nextLayer, Exchange exchange, Request request)
        {
            if (exchange.CurrentRequest.Token == null)
                throw new InvalidOperationException("Received requests's token cannot be null, use byte[0] for empty tokens");
            base.ReceiveRequest(nextLayer, exchange, request);
        }

        /// <inheritdoc/>
        public override void ReceiveResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            if (response.Token == null)
                throw new InvalidOperationException("Received response's token cannot be null, use byte[0] for empty tokens");
            base.ReceiveResponse(nextLayer, exchange, response);
        }

        private Byte[] NewToken()
        {
            // RFC 7252 section 5.3.1 recommends nontrivial randomized tokens
            // without transport security. There is no wrapping counter or token
            // history here. IChannel currently exposes no DTLS session/key
            // identity: randomness alone does not establish RFC 9175 binding.
            return RandomNumberGenerator.GetBytes(8);
        }
    }
}
