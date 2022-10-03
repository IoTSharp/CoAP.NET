namespace WorldDirect.CoAP.V1.Options
{
    public class ContentFormatFactory : IOptionFactory
    {
        private readonly ContentFormatRegistry registry;

        public ContentFormatFactory(ContentFormatRegistry registry)
        {
            this.registry = registry;
        }

        public CoapOption Create(OptionData src)
        {
            var id = src.UIntValue;
            return this.registry.Get(c => c.Id.Equals(id));
        }

        public int Number => ContentFormat.NUMBER;
    }
}