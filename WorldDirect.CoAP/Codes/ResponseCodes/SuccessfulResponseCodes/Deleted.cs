// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Deleted : SuccessfulResponseCode
    {
        public Deleted()
            : base(new CodeDetail((UInt5)2))
        {
        }

        public override string ToString() => $"Deleted ({base.ToString()})";
    }
}
