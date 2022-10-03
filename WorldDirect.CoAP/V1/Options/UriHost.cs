// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class UriHost : StringOptionFormat
    {
        public const ushort NUMBER = 3;

        public UriHost(string value)
            : base(NUMBER, value, 1, 255)
        {
        }
    }
}
