// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP
{
    using System;

    public interface IReader<TResult>
    {
        int Read(ReadOnlyMemory<byte> value, out TResult result);
    }
}