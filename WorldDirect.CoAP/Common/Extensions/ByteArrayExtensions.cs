// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common.Extensions
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Contains extension methods for <see cref="byte"/>[].
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Gets a <see cref="byte"/>[] that represents the specified hex string.
        /// </summary>
        /// <param name="value">The hex <see cref="string"/>.</param>
        /// <returns>A <see cref="byte"/>[] that represents the specified hex string.</returns>
        public static byte[] FromHexString(string value)
        {
            var subStrings = value.Split(' ');
            var byteArray = subStrings
                .Select(s => byte.Parse(s, NumberStyles.HexNumber))
                .ToArray();
            return byteArray;
        }

        /// <summary>
        /// Gets the <see cref="string" /> representation from the current <see cref="byte" />[] with the specified <paramref name="separator"/>.
        /// </summary>
        /// <param name="value">The current <see cref="byte" />[].</param>
        /// <param name="separator">The specified separator that is used to separate each value from the <see cref="byte" />[].</param>
        /// <returns>
        /// A <see cref="string" /> that represents the content of the <paramref name="value"/> separated by the specified <paramref name="separator"/>.
        /// </returns>
        public static string ToString(this byte[] value, char separator)
        {
            var builder = new StringBuilder();
            foreach (var item in value)
            {
                builder.Append(item.ToString("X"));
                builder.Append(separator);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Aligns the specified <see cref="ReadOnlySpan{T}"/> to the specified amount of <paramref name="bits"/>.
        /// </summary>
        /// <param name="value">The <see cref="ReadOnlySpan{T}"/> that should be aligned to the specified amount of <paramref name="bits"/>.</param>
        /// <param name="bits">The amount of bits of the resulting <see cref="ReadOnlySpan{T}"/>.</param>
        /// <returns>A <see cref="byte"/>[] that is aligned to the specified amount of <paramref name="bits"/> and holds the content of the <paramref name="value"/>.</returns>
        /// <remarks>
        /// 'Align' means that a new byte array of the specified <paramref name="bits"/> will be created and filled with zeros (0) at default.
        /// In the next step, the newly created byte array will be filled with the content of the specified <paramref name="value"/>.
        /// </remarks>
        public static byte[] Align(this byte[] value, int bits)
        {
            var usedBytes = ByteArrayExtensions.CalculateBytes(bits);

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

        public static byte[] RemoveLeadingZeros(this byte[] value)
        {
            var x = value
                .Where(b => !b.Equals(0))
                .ToArray();
            return x;
        }

        private static int CalculateBytes(int bits)
        {
            if (bits % 8 == 0)
            {
                return bits / 8;
            }

            var bytes = 0;
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
