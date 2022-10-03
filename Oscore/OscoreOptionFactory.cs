namespace Oscore
{
    using WorldDirect.CoAP;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.V1.Options;

    public class OscoreOptionFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            var length = (UInt3)(src.Value[0] >> 6);
            return new OscoreOption(src.Value, new OscoreOptionValue()
            {
                KeyIdFlag = false,
                Length = length,
            });
        }

        public int Number => OscoreOption.NUMBER;
    }
}