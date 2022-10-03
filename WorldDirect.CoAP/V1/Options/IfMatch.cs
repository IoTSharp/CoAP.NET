// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class IfMatch : OpaqueOptionFormat
    {
        public const ushort NUMBER = 1;

        public IfMatch(byte[] value)
            : base(NUMBER, value, 0, 8)
        {
        }
    }
}
