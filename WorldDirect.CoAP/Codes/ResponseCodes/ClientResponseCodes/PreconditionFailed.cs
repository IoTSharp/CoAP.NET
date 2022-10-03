// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class PreconditionFailed : ClientResponseCode
    {
        public PreconditionFailed()
            : base(new CodeDetail((UInt5)12))
        {
        }

        public override string ToString() => $"Precondition Failed ({base.ToString()})";
    }
}
