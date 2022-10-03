// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Changed : SuccessfulResponseCode
    {
        public Changed()
            : base(new CodeDetail((UInt5)4))
        {
        }

        public override string ToString() => $"Changed ({base.ToString()})";
    }
}
