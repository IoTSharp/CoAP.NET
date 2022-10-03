// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP
{
    using WorldDirect.CoAP.V1.Options;

    public interface IOptionFactory
    {
        CoapOption Create(OptionData src);

        int Number { get; }
    }
}
