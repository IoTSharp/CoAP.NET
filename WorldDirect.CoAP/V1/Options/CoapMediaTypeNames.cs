namespace WorldDirect.CoAP.V1.Options
{
    public static class CoapMediaTypeNames
    {
        public static class Text
        {
            public const string TextPlainUtf8 = "text/plain; charset=utf-8";
        }

        public static class Application
        {
            public const string LinkFormat = "application/link-format";

            public const string Xml = "application/xml";

            public const string OctetStream = "application/octet-stream";

            public const string Exi = "application/exi";

            public const string Json = "application/json";
        }
    }
}