namespace WorldDirect.CoAP.V1.Options
{
    using System.Text;

    public sealed class ExiFormat : ContentFormat
    {
        public const uint NUMBER = 47;

        public ExiFormat()
            : base(NUMBER, CoapMediaTypeNames.Application.Exi)
        {
        }
    }
}
