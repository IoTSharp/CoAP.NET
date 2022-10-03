// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using CoAP.Common;

    public class NotImplemented : ServerResponseCode
    {
        public NotImplemented()
            : base(new CodeDetail((UInt5)1))
        {
        }

        public override string ToString() => $"Not implemented ({base.ToString()})";
    }
}
