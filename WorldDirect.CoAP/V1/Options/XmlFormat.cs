namespace WorldDirect.CoAP.V1.Options
{
    public sealed class XmlFormat : ContentFormat
    {
        public XmlFormat()
            : base(41, CoapMediaTypeNames.Application.Xml)
        {
        }
    }
}
