// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1
{
    using System;
    using System.Buffers.Binary;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.Common.Extensions;
    using WorldDirect.CoAP.V1.Messages;

    public class TokenReader : IReader<CoapToken>
    {
        public int Read(ReadOnlyMemory<byte> value, out CoapToken result)
        {
            var tokenValue = BinaryPrimitives.ReadUInt64BigEndian(value.Span.Align(64));
            result = new CoapToken(tokenValue, (CoapTokenLength)(UInt4)value.Length);
            return value.Length;
        }
    }
}
