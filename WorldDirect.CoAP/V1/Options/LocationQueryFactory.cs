namespace WorldDirect.CoAP.V1.Options
{
    public class LocationQueryFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new LocationQuery(src.StringValue);
        }

        public int Number => LocationQuery.NUMBER;
    }
}