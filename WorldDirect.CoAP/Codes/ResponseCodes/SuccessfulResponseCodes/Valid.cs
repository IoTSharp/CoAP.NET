// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Valid : SuccessfulResponseCode
    {
        public Valid()
            : base(new CodeDetail((UInt5)3))
        {
        }

        public override string ToString() => $"Valid ({base.ToString()})";
    }
}
