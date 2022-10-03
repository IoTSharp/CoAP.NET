namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class ProxyUri : StringOptionFormat
    {
        public const ushort NUMBER = 35;

        public ProxyUri(string value)
            : base(NUMBER, value, 1, 1034)
        {
        }
    }
}
