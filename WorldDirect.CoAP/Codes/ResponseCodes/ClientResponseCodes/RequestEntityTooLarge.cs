// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using WorldDirect.CoAP.Common;

    public class RequestEntityTooLarge : ClientResponseCode
    {
        public RequestEntityTooLarge()
            : base(new CodeDetail((UInt5)13))
        {
        }

        public override string ToString() => $"Request Entity Too Large ({base.ToString()})";
    }
}
