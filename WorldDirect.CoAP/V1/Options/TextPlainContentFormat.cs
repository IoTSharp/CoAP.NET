// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    public sealed class TextPlainContentFormat : ContentFormat
    {
        public TextPlainContentFormat()
            : base(0, "text/plain; charset=utf-8")
        {
        }
    }
}
