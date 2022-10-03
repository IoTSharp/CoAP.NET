// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class NotAcceptable : ClientResponseCode
    {
        public NotAcceptable()
            : base(new CodeDetail((UInt5)6))
        {
        }

        public override string ToString() => $"NotAcceptable ({base.ToString()})";
    }
}
