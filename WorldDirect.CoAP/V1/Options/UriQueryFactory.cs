namespace WorldDirect.CoAP.V1.Options
{
    public class UriQueryFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new UriQuery(src.StringValue);
        }

        public int Number => UriQuery.NUMBER;
    }
}