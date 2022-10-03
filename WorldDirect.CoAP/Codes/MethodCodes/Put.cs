// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using CoAP.Common;

    public class Put : MethodCode
    {
        public Put()
            : base(new CodeDetail((UInt5)3))
        {
        }

        public override string ToString() => $"PUT {base.ToString()}";
    }
}
