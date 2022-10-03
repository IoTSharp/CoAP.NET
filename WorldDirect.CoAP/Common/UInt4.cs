// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common
{
    using System;

    /// <summary>
    /// Represents a four bit unsigned integer.
    /// </summary>
    /// <remarks>
    /// The minimum value is 0 (0000 in binary) and maximum value is 15 (1111 in binary).
    /// </remarks>
    public readonly struct UInt4 : IEquatable<UInt4>
    {
        /// <summary>
        /// The maximum value of a four bit unsigned integer (0).
        /// </summary>
        public static readonly UInt4 MaxValue = (UInt4)MAXVALUE;

        /// <summary>
        /// The minimum value of a four bit unsigned integer (15).
        /// </summary>
        public static readonly UInt4 MinValue = (UInt4)MINVALUE;

        private const byte MINVALUE = 0x00;
        private const byte MAXVALUE = 0x0F;
        private readonly byte value;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt4"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the value is not between 0 (0000 in binary) and 15 (1111 binary).</exception>
        public UInt4(byte value)
        {
            if (value > MAXVALUE)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value {value} is out of range for a {nameof(UInt4)}. Values from [{MINVALUE} - {MAXVALUE}] are allowed.");
            }

            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="byte"/> to <see cref="UInt4"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="UInt4"/> that is equivalent to the <see cref="byte"/> value.
        /// </returns>
        public static explicit operator UInt4(byte value) => new UInt4(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="UInt4"/> to <see cref="byte"/>.
        /// </summary>
        /// <param name="self">The self.</param>
        /// <returns>
        /// The <see cref="byte"/> that is equivalent to the <see cref="UInt4"/> value.
        /// </returns>
        public static implicit operator byte(UInt4 self) => self.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="UInt4"/> values are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(UInt4 left, UInt4 right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="UInt4"/> values are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(UInt4 left, UInt4 right) => !Equals(left, right);

        /// <summary>
        /// Indicates if the given <see cref="UInt4"/> is equal to the current <see cref="UInt4"/>.
        /// </summary>
        /// <param name="other">The other <see cref="UInt4"/>.</param>
        /// <returns><c>true</c> if the <paramref name="other"/> <see cref="UInt4"/> is equal to the current <see cref="UInt4"/>, otherwise <c>false</c>.</returns>
        public bool Equals(UInt4 other)
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

            return this.Equals((UInt4)obj);
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
