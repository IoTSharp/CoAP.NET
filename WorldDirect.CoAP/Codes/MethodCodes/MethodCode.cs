// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    public abstract class MethodCode : RequestCode
    {
        protected MethodCode(CodeDetail detail)
            : base(detail)
        {
        }
    }
}
