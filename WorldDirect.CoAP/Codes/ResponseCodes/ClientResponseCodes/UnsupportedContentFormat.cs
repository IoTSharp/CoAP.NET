// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class UnsupportedContentFormat : ClientResponseCode
    {
        public UnsupportedContentFormat()
            : base(new CodeDetail((UInt5)15))
        {
        }

        public override string ToString() => $"Unsupported Content-Format ({base.ToString()})";
    }
}
