// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Forbidden : ClientResponseCode
    {
        public Forbidden()
            : base(new CodeDetail((UInt5)3))
        {
        }

        public override string ToString() => $"Forbidden ({base.ToString()})";
    }
}
