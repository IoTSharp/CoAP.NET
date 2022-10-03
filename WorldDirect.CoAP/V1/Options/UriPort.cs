// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class UriPort : UIntOptionFormat
    {
        public const ushort NUMBER = 7;

        public UriPort(uint value)
            : base(NUMBER, value, 0, 2)
        {
        }
    }
}
