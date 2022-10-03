// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using WorldDirect.CoAP.Common;

    public class Post : MethodCode
    {
        public Post()
            : base(new CodeDetail((UInt5)2))
        {
        }

        public override string ToString() => $"POST {base.ToString()}";
    }
}
