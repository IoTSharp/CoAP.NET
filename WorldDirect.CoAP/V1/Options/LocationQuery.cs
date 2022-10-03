namespace WorldDirect.CoAP.V1.Options
{
    using System;

    public class LocationQuery : StringOptionFormat
    {
        public const ushort NUMBER = 20;

        public LocationQuery(string value)
            : base(NUMBER, value, 0, 255)
        {
        }
    }
}
