// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public abstract class ClientResponseCode : ResponseCode
    {
        protected ClientResponseCode(CodeDetail detail)
            : base(new CodeClass((UInt3)4), detail)
        {
        }
    }
}
