// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common
{
    using System;

    /// <summary>
    /// Represents a five bit unsigned integer.
    /// </summary>
    /// <remarks>
    /// The minimum value is 0 (00000 in binary) and maximum value is 31 (11111 in binary).
    /// </remarks>
    public readonly struct UInt5 : IEquatable<UInt5>, IFormattable
    {
        /// <summary>
        /// The minimum value of a five bit unsigned integer (0).
        /// </summary>
        public static readonly UInt5 MinValue = (UInt5)MINVALUE;

        /// <summary>
        /// The maximum value of a five bit unsigned integer (31).
        /// </summary>
        public static readonly UInt5 MaxValue = (UInt5)MAXVALUE;

        private const byte MINVALUE = 0x00;
        private const byte MAXVALUE = 0x1F;
        private readonly byte value;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt5"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the value is not between 0 (00000 in binary) and 31 (11111 binary).</exception>
        public UInt5(byte value)
        {
            if (value > MAXVALUE)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value {value} is out of range for a {nameof(UInt5)}. Values from [{MINVALUE} - {MAXVALUE}] are allowed.");
            }

            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="byte"/> to <see cref="UInt5"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="UInt5"/> that is equivalent to the <see cref="byte"/> <paramref name="value"/>.
        /// </returns>
        public static explicit operator UInt5(byte value) => new UInt5(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="UInt5"/> to <see cref="byte"/>.
        /// </summary>
        /// <param name="self">The self.</param>
        /// <returns>
        /// The <see cref="byte"/> that is equivalent to the <see cref="UInt5"/> value.
        /// </returns>
        public static implicit operator byte(UInt5 self) => self.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both values are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(UInt5 left, UInt5 right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both values are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(UInt5 left, UInt5 right) => !Equals(left, right);

        /// <summary>
        /// Indicates if the given <see cref="UInt5"/> is equal to the current <see cref="UInt5"/>.
        /// </summary>
        /// <param name="other">The other <see cref="UInt5"/>.</param>
        /// <returns><c>true</c> if the <paramref name="other"/> <see cref="UInt5"/> is equal to the current <see cref="UInt5"/>, otherwise <c>false</c>.</returns>
        public bool Equals(UInt5 other)
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

            return this.Equals((UInt5)obj);
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

        /// <inheritdoc />
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return this.value.ToString(format, formatProvider);
        }
    }
}
