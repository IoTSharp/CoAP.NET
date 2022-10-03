namespace WorldDirect.CoAP.V1.Options
{
    public class IfNoneMatchFactory : IOptionFactory
    {
        public CoapOption Create(OptionData src)
        {
            return new IfNoneMatch();
        }

        public int Number => IfNoneMatch.NUMBER;
    }
}