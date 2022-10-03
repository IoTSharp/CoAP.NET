namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class UriQuery : StringOptionFormat
    {
        public const ushort NUMBER = 15;

        public UriQuery(string value)
            : base(NUMBER, value, 0, 255)
        {
        }
    }
}
