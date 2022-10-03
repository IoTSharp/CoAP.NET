namespace WorldDirect.CoAP.V1.Options
{
    public class UriPortFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new UriPort(src.UIntValue);
        }

        public int Number => UriPort.NUMBER;
    }
}