using System;

namespace Oscore
{
    using WorldDirect.CoAP.V1.Options;

    public class OscoreOption : CoapOption
    {
        public const ushort NUMBER = 9;

        public OscoreOption(byte[] value, OscoreOptionValue optionValue)
            : base(NUMBER, value, 0, 255)
        {
            this.Value = optionValue;
        }

        public OscoreOptionValue Value { get; }
    }
}
