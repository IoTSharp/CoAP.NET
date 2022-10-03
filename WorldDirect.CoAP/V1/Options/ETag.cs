// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class ETag : OpaqueOptionFormat
    {
        public const ushort NUMBER = 4;

        public ETag(byte[] value)
            : base(NUMBER, value, 1, 8)
        {
        }
    }
}
