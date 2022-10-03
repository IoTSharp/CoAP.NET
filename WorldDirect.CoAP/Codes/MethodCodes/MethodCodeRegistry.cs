// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using System.Collections.Generic;
    using WorldDirect.CoAP.Common;

    public class MethodCodeRegistry : Registry<MethodCode>
    {
        public MethodCodeRegistry()
        {
            this.Elements = new List<MethodCode>()
            {
                new Get(),
                new Post(),
                new Put(),
                new Delete(),
            };
        }

        public MethodCodeRegistry(IEnumerable<MethodCode> codes)
            : base(codes)
        {
        }
    }
}
