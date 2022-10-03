namespace WorldDirect.CoAP.V1.Options
{
    public class ProxySchemeFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new ProxyScheme(src.StringValue);
        }

        public int Number => ProxyScheme.NUMBER;
    }
}