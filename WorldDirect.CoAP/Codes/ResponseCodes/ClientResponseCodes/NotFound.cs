// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class NotFound : ClientResponseCode
    {
        public NotFound()
            : base(new CodeDetail((UInt5)4))
        {
        }

        public override string ToString() => $"Not Found ({base.ToString()})";
    }
}
