// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using WorldDirect.CoAP.Common;

    public abstract class RequestCode : CoapCode
    {
        protected RequestCode(CodeDetail detail)
            : base(new CodeClass((UInt3)0), detail)
        {
        }
    }
}
