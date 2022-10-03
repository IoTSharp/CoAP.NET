// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using CoAP.Common;

    public class GatewayTimeout : ServerResponseCode
    {
        public GatewayTimeout()
            : base(new CodeDetail((UInt5)3))
        {
        }

        public override string ToString() => $"Gateway timeout ({base.ToString()})";
    }
}
