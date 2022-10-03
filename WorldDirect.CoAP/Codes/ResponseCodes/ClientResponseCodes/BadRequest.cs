// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class BadRequest : ClientResponseCode
    {
        public BadRequest()
            : base(new CodeDetail((UInt5)0))
        {
        }

        public override string ToString() => $"Bad Request ({base.ToString()})";
    }
}
