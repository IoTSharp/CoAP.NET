// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class BadOption : ClientResponseCode
    {
        public BadOption()
            : base(new CodeDetail((UInt5)2))
        {
        }

        public override string ToString() => $"Bad Option ({base.ToString()})";
    }
}
