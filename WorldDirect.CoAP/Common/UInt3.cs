// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common
{
    using System;

    /// <summary>
    /// Represents a three bit unsigned integer.
    /// </summary>
    /// <remarks>
    /// The minimum value is 0 (000 in binary) and maximum value is 7 (111 in binary).
    /// </remarks>
    public readonly struct UInt3 : IEquatable<UInt3>
    {
        /// <summary>
        /// The maximum value of a three bit unsigned integer (0).
        /// </summary>
        public static readonly UInt3 MaxValue = (UInt3)MAXVALUE;

        /// <summary>
        /// The minimum value of a three bit unsigned integer (7).
        /// </summary>
        public static readonly UInt3 MinValue = (UInt3)MINVALUE;

        private const byte MINVALUE = 0x00;
        private const byte MAXVALUE = 0x07;
        private readonly byte value;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt3"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the value is not between 0 (000 in binary) and 7 (111 binary).</exception>
        public UInt3(byte value)
        {
            if (value > MAXVALUE)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value {value} is out of range for a {nameof(UInt4)}. Values from [{MINVALUE} - {MAXVALUE}] are allowed.");
            }

            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="byte"/> to <see cref="UInt3"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="UInt3"/> that is equivalent to the <see cref="byte"/> <paramref name="value"/>.
        /// </returns>
        public static explicit operator UInt3(byte value) => new UInt3(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="UInt3"/> to <see cref="byte"/>.
        /// </summary>
        /// <param name="self">The self.</param>
        /// <returns>
        /// The <see cref="byte"/> that is equivalent to the <see cref="UInt3"/> value.
        /// </returns>
        public static implicit operator byte(UInt3 self) => self.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="UInt3"/> values are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(UInt3 left, UInt3 right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="UInt3"/> values are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(UInt3 left, UInt3 right) => !Equals(left, right);

        /// <summary>
        /// Indicates if the given <see cref="UInt3"/> is equal to the current <see cref="UInt3"/>.
        /// </summary>
        /// <param name="other">The other <see cref="UInt3"/>.</param>
        /// <returns><c>true</c> if the <paramref name="other"/> <see cref="UInt2"/> is equal to the current <see cref="UInt3"/>, otherwise <c>false</c>.</returns>
        public bool Equals(UInt3 other)
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

            return this.Equals((UInt3)obj);
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
