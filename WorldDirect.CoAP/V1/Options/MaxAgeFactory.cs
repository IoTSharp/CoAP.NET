namespace WorldDirect.CoAP.V1.Options
{
    public class MaxAgeFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new MaxAge(src.UIntValue);
        }

        public int Number => MaxAge.NUMBER;
    }
}