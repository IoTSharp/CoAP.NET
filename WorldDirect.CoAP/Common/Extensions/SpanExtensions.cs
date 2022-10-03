namespace WorldDirect.CoAP.Common.Extensions
{
    using System;
    using System.Linq.Expressions;

    public static class SpanExtensions
    {
        /// <summary>
        /// Aligns the specified <see cref="Span{T}"/> to the specified amount of bits <paramref name="bits"/>.
        /// </summary>
        /// <param name="value">The <see cref="Span{T}"/> that should be aligned to the specified amount of <paramref name="bits"/>.</param>
        /// <param name="bits">The amount of bits of the resulting <see cref="Span{T}"/>.</param>
        /// <returns>A <see cref="Span{T}"/> that is aligned to the specified amount of <paramref name="bits"/> and holds the content of the <paramref name="value"/>.</returns>
        /// <remarks>
        /// 'Align' means that a new byte array of the specified <paramref name="bits"/> will be created and filled with zeros (0) at default.
        /// In the next step, the newly created byte array will be filled with the content of the specified <paramref name="value"/>.
        /// </remarks>
        public static Span<byte> Align(this Span<byte> value, int bits)
        {
            var usedBytes = SpanExtensions.CalculateBytes(bits);

            // Initialize buffer with the specified size and set every item to zero (0) as default.
            var buffer = new byte[usedBytes];

            // Check if the span fits into the buffer
            if (value.Length > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can not align the specified span to the given amount of bits.");
            }

            // Fill the content of the Span into the buffer.
            int offset = usedBytes - value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                buffer[i + offset] = value[i];
            }

            return buffer;
        }

        /// <summary>
        /// Aligns the specified <see cref="ReadOnlySpan{T}"/> to the specified amount of bits <paramref name="bits"/>.
        /// </summary>
        /// <param name="value">The <see cref="ReadOnlySpan{T}"/> that should be aligned to the specified amount of <paramref name="bits"/>.</param>
        /// <param name="bits">The amount of bits of the resulting <see cref="Span{T}"/>.</param>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> that is aligned to the specified amount of <paramref name="bits"/> and holds the content of the <paramref name="value"/>.</returns>
        /// <remarks>
        /// 'Align' means that a new byte array of the specified <paramref name="bits"/> will be created and filled with zeros (0) at default.
        /// In the next step, the newly created byte array will be filled with the content of the specified <paramref name="value"/>.
        /// </remarks>
        public static ReadOnlySpan<byte> Align(this ReadOnlySpan<byte> value, int bits)
        {
            var usedBytes = SpanExtensions.CalculateBytes(bits);

            // Initialize buffer with the specified size and set every item to zero (0) as default.
            var buffer = new byte[usedBytes];

            // Check if the span fits into the buffer
            if (value.Length > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can not align the specified span to the given amount of bits.");
            }

            // Fill the content of the Span into the buffer.
            int offset = usedBytes - value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                buffer[i + offset] = value[i];
            }

            return buffer;
        }

        /// <summary>
        /// Reverses the content of the specified <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The <see cref="Span{T}"/> that should be reversed.</param>
        /// <returns>A <see cref="Span{T}"/> that contains the reversed content of the specified <paramref name="value"/>.</returns>
        /// <remarks>
        /// This operation leads into a memory allocation of the specified <paramref name="value"/> for reversing it.
        /// </remarks>
        public static Span<byte> Reverse(this Span<byte> value)
        {
            var offset = value.Length - 1;
            var buffer = new byte[value.Length];
            for (int i = offset; i >= 0; i--)
            {
                buffer[i] = value[offset - i];
            }

            return buffer;
        }

        /// <summary>
        /// Reverses the content of the specified <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The <see cref="ReadOnlySpan{T}"/> that should be reversed.</param>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> that contains the reversed content of the specified <paramref name="value"/>.</returns>
        /// <remarks>
        /// This operation leads into a memory allocation of the specified <paramref name="value"/> for reversing it.
        /// </remarks>
        public static ReadOnlySpan<byte> Reverse(this ReadOnlySpan<byte> value)
        {
            var offset = value.Length - 1;
            var buffer = new byte[value.Length];
            for (int i = offset; i >= 0; i--)
            {
                buffer[i] = value[offset - i];
            }

            return buffer;
        }

        private static int CalculateBytes(int bits)
        {
            if (bits % 8 == 0)
            {
                return bits / 8;
            }

            var bytes = 1;
            var lowerBound = 0;
            var upperBound = 8;

            while (bits < lowerBound || bits > upperBound)
            {
                upperBound = lowerBound + 8;
                bytes++;
                lowerBound += 8;
            }

            return bytes;
        }
    }
}
