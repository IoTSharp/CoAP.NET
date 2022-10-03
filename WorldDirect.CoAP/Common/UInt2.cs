// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common
{
    using System;

    /// <summary>
    /// Represents a two bit unsigned integer.
    /// </summary>
    /// <remarks>
    /// The minimum value is 0 (00 in binary) and maximum value is 4 (11 in binary).
    /// </remarks>
    public readonly struct UInt2 : IEquatable<UInt2>
    {
        /// <summary>
        /// The maximum value of a two bit unsigned integer (0).
        /// </summary>
        public static readonly UInt2 MaxValue = (UInt2)MAXVALUE;

        /// <summary>
        /// The minimum value of a two bit unsigned integer (4).
        /// </summary>
        public static readonly UInt2 MinValue = (UInt2)MINVALUE;

        private const byte MINVALUE = 0x00;
        private const byte MAXVALUE = 0x03;
        private readonly byte value;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt2"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the value is not between 0 (00 in binary) and 4 (11 binary).</exception>
        public UInt2(byte value)
        {
            if (value > MAXVALUE)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value {value} is out of range for a {nameof(UInt2)}. Values from [{MINVALUE} - {MAXVALUE}] are allowed.");
            }

            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="byte"/> to <see cref="UInt2"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="UInt2"/> that is equivalent to the given <see cref="byte"/> <paramref name="value"/>.
        /// </returns>
        public static explicit operator UInt2(byte value) => new UInt2(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="UInt2"/> to <see cref="byte"/>.
        /// </summary>
        /// <param name="self">The self.</param>
        /// <returns>
        /// A <see cref="byte"/> that is equivalent to the given <see cref="UInt2"/> value.
        /// </returns>
        public static implicit operator byte(UInt2 self) => self.value;

        public static bool operator ==(UInt2 left, UInt2 right) => Equals(left, right);

        public static bool operator !=(UInt2 left, UInt2 right) => !Equals(left, right);

        /// <summary>
        /// Indicates if the given <see cref="UInt2"/> is equal to the current <see cref="UInt2"/>.
        /// </summary>
        /// <param name="other">The other <see cref="UInt2"/>.</param>
        /// <returns><c>true</c> if the <paramref name="other"/> <see cref="UInt2"/> is equal to the current <see cref="UInt2"/>, otherwise <c>false</c>.</returns>
        public bool Equals(UInt2 other)
        {
            return this.value == other.value;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((UInt2)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.value.ToString("D");
        }
    }
}
