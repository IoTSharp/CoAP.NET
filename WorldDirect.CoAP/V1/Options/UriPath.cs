namespace WorldDirect.CoAP.V1.Options
{
    using System;
    using System.Text;

    public class UriPath : StringOptionFormat
    {
        public const ushort NUMBER = 11;

        public UriPath(string value)
            : base(NUMBER, value, 0, 255)
        {
        }
    }
}
