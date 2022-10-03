// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using CoAP.Common;

    public class BadGateway : ServerResponseCode
    {
        public BadGateway()
            : base(new CodeDetail((UInt5)2))
        {
        }

        public override string ToString() => $"Bad gateway ({base.ToString()})";
    }
}
