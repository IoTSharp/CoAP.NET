// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes
{
    using System.Collections.Generic;
    using CoAP.Common;

    public class SuccessfulResponseCodeRegistry : Registry<SuccessfulResponseCode>
    {
        public SuccessfulResponseCodeRegistry()
        {
            this.Elements = new List<SuccessfulResponseCode>()
            {
                new Created(),
                new Deleted(),
                new Valid(),
                new Changed(),
                new Content(),
            };
        }

        public SuccessfulResponseCodeRegistry(IEnumerable<SuccessfulResponseCode> codes)
            : base(codes)
        {
        }
    }
}
