// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    public abstract class ResponseCode : CoapCode
    {
        public ResponseCode(CodeClass @class, CodeDetail detail)
            : base(@class, detail)
        {
        }
    }
}
