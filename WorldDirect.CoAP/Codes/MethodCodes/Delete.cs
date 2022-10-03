// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using WorldDirect.CoAP.Common;

    public class Delete : MethodCode
    {
        public Delete()
            : base(new CodeDetail((UInt5)4))
        {
        }

        public override string ToString() => $"Delete {base.ToString()}";
    }
}
