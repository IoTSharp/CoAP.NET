namespace WorldDirect.CoAP.V1.Options
{
    public sealed class LinkFormat : ContentFormat
    {
        public LinkFormat()
            : base(40, CoapMediaTypeNames.Application.LinkFormat)
        {
        }
    }
}
