// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.ResponseCodes.ServerResponseCodes
{
    using System.Collections.Generic;
    using CoAP.Common;

    public class ServerResponseCodeRegistry : Registry<ServerResponseCode>
    {
        public ServerResponseCodeRegistry()
        {
            this.Elements = new List<ServerResponseCode>()
            {
                new InternalServerError(),
                new BadGateway(),
                new ServiceUnavailable(),
                new GatewayTimeout(),
                new ProxyingNotSupported(),
            };
        }

        public ServerResponseCodeRegistry(IEnumerable<ServerResponseCode> codes)
            : base(codes)
        {
        }
    }
}
