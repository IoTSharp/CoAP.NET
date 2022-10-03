// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using WorldDirect.CoAP.Common;

    public abstract class ServerResponseCode : ResponseCode
    {
        protected ServerResponseCode(CodeDetail detail)
            : base(new CodeClass((UInt3)5), detail)
        {
        }
    }
}
