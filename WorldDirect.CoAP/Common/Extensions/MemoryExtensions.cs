namespace WorldDirect.CoAP.Common.Extensions
{
    using System;

    public static class MemoryExtensions
    {
        public static ReadOnlyMemory<byte> Reverse(this ReadOnlyMemory<byte> value)
        {
            var index = value.Length;
            var buffer = new byte[value.Length];
            foreach (var item in value.Span)
            {
                buffer[--index] = item;
            }

            return buffer;
        }
    }
}
