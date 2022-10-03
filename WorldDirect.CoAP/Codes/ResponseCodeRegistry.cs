// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using System.Collections.Generic;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.Codes.ResponseCodes.ClientResponseCodes;
    using WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes;
    using WorldDirect.CoAP.Codes.ResponseCodes.SuccessfulResponseCodes;

    public class ResponseCodeRegistry : Registry<ResponseCode>
    {
        public ResponseCodeRegistry()
        {
            this.Elements.AddRange(new SuccessfulResponseCodeRegistry());
            this.Elements.AddRange(new ServerResponseCodeRegistry());
            this.Elements.AddRange(new ClientResponseCodeRegistry());
        }

        public ResponseCodeRegistry(IEnumerable<ResponseCode> codes)
            : base(codes)
        {
        }

        public ResponseCodeRegistry(
            SuccessfulResponseCodeRegistry successfulResponses,
            ServerResponseCodeRegistry serverResponses,
            ClientResponseCodeRegistry clientResponses)
        {
            this.Elements.AddRange(successfulResponses);
            this.Elements.AddRange(serverResponses);
            this.Elements.AddRange(clientResponses);
        }
    }
}
