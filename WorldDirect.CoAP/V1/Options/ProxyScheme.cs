namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class ProxyScheme : StringOptionFormat
    {
        public const ushort NUMBER = 39;

        public ProxyScheme(string value)
            : base(NUMBER, value, 1, 255)
        {
        }
    }
}
