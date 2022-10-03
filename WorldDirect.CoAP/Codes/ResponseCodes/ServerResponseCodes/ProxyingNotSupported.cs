// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using CoAP.Common;

    public class ProxyingNotSupported : ServerResponseCode
    {
        public ProxyingNotSupported()
            : base(new CodeDetail((UInt5)3))
        {
        }

        public override string ToString() => $"Proxying not supported ({base.ToString()})";
    }
}
