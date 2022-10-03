namespace WorldDirect.CoAP.V1.Options
{
    public class UriPathFactory : IOptionFactory
    {
        /// <inheritdoc />
        public CoapOption Create(OptionData src)
        {
            return new UriPath(src.StringValue);
        }

        /// <inheritdoc />
        public int Number => UriPath.NUMBER;
    }
}