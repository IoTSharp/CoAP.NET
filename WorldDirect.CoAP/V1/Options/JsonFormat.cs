namespace WorldDirect.CoAP.V1.Options
{
    using System.Text;

    public sealed class JsonFormat : ContentFormat
    {
        public JsonFormat()
            : base(50, CoapMediaTypeNames.Application.Json)
        {
        }
    }
}
