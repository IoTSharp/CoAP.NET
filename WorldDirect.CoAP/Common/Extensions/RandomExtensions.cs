// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common.Extensions
{
    using System;

    public static class RandomExtensions
    {
        public static ulong NextULong(this Random random, int arraySize)
        {
            var buffer = new byte[arraySize];
            random.NextBytes(buffer);
            var value = BitConverter.ToUInt64(buffer, 0);
            return value;
        }
    }
}
