namespace WorldDirect.CoAP.V1.Options
{
    public class MaxAge : UIntOptionFormat
    {
        public const ushort NUMBER = 14;

        public MaxAge(uint value)
            : base(NUMBER, value, 0, 4)
        {
        }
    }
}
