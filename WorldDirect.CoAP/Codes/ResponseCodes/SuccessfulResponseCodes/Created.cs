// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Created : SuccessfulResponseCode
    {
        public Created()
            : base(new CodeDetail((UInt5)1))
        {
        }

        public override string ToString() => $"Created ({base.ToString()})";
    }
}
