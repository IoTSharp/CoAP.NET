// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes.Common
{
    using WorldDirect.CoAP.V1.Options;

    public class UnknownOption : CoapOption
    {
        public UnknownOption(ushort number, byte[] rawValue)
            : base(number, rawValue, 0, uint.MaxValue)
        {
        }
    }
}
