namespace Oscore
{
    using WorldDirect.CoAP.Common;

    public struct OscoreOptionValue
    {
        public UInt3 Length { get; set; }

        public bool KeyIdFlag { get; set; }

        public override string ToString() => $"{Length} - {KeyIdFlag}";
    }
}