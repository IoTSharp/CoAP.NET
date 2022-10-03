// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes
{
    using System.Collections.Generic;
    using WorldDirect.CoAP.Common;

    public class ClientResponseCodeRegistry : Registry<ClientResponseCode>
    {
        public ClientResponseCodeRegistry()
        {
            this.Elements = new List<ClientResponseCode>()
            {
                new BadRequest(),
                new Unauthorized(),
                new BadOption(),
                new Forbidden(),
                new NotFound(),
                new MethodNotAllowed(),
                new NotAcceptable(),
                new PreconditionFailed(),
                new RequestEntityTooLarge(),
                new UnsupportedContentFormat(),
            };
        }

        public ClientResponseCodeRegistry(IEnumerable<ClientResponseCode> codes)
            : base(codes)
        {
        }
    }
}
