// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using CoAP.Common;

    public class Get : MethodCode
    {
        public Get()
            : base(new CodeDetail((UInt5)1))
        {
        }

        public override string ToString() => $"GET {base.ToString()}";
    }
}
