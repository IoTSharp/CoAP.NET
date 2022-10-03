// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class MethodNotAllowed : ClientResponseCode
    {
        public MethodNotAllowed()
            : base(new CodeDetail((UInt5)5))
        {
        }

        public override string ToString() => $"Method Not Allowed ({base.ToString()})";
    }
}
