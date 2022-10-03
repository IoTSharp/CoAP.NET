namespace WorldDirect.CoAP.V1.Options
{
    public class ETagFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new ETag(src.Value);
        }

        public int Number => ETag.NUMBER;
    }
}