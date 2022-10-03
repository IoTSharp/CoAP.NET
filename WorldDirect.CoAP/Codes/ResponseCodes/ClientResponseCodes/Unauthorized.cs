// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Unauthorized : ClientResponseCode
    {
        public Unauthorized()
            : base(new CodeDetail((UInt5)1))
        {
        }

        public override string ToString() => $"Unauthorized ({base.ToString()})";
    }
}
