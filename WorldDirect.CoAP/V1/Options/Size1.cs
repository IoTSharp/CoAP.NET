namespace WorldDirect.CoAP.V1.Options
{
    public class Size1 : UIntOptionFormat
    {
        public const ushort NUMBER = 60;

        public Size1(uint value)
            : base(NUMBER, value, 0, 4)
        {
        }
    }
}
