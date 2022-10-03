// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class Content : SuccessfulResponseCode
    {
        public Content()
            : base(new CodeDetail((UInt5)5))
        {
        }

        public override string ToString() => $"Content ({base.ToString()})";
    }
}
