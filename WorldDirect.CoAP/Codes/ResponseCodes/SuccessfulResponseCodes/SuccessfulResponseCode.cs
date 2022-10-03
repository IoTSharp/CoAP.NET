// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using WorldDirect.CoAP.Common;

    public abstract class SuccessfulResponseCode : ResponseCode
    {
        protected SuccessfulResponseCode(CodeDetail detail)
            : base(new CodeClass((UInt3)2), detail)
        {
        }
    }
}
