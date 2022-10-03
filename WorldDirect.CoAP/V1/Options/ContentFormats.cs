namespace WorldDirect.CoAP.V1.Options
{
    public sealed class ContentFormats
    {
        public static readonly ContentFormat TextPlainContentFormat = new TextPlainContentFormat();

        public static readonly ContentFormat LinkContentFormat = new LinkFormat();

        public static readonly ContentFormat XmlContentFormat = new XmlFormat();

        public static readonly ContentFormat OctetContentFormat = new OctetStreamFormat();

        public static readonly ContentFormat ExiContentFormat = new ExiFormat();

        public static readonly ContentFormat JsonContentFormat = new JsonFormat();
    }
}