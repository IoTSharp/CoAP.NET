// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using CoAP.Common;

    public class InternalServerError : ServerResponseCode
    {
        public InternalServerError()
            : base(new CodeDetail((UInt5)0))
        {
        }

        public override string ToString() => $"Internal server error ({base.ToString()})";
    }
}
