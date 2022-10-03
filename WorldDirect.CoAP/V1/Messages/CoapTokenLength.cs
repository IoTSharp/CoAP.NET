// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Messages
{
    using WorldDirect.CoAP.Common;

    /// <summary>
    /// Represents the token length field (TKL) in a CoAP message.
    /// It indicates the length of the variable-length Token field.
    /// </summary>
    public readonly struct CoapTokenLength
    {
        private readonly UInt4 value;

        public static readonly CoapTokenLength Default = new CoapTokenLength((UInt4)0);

        /// <summary>
        /// Initializes a new instance of the <see cref="CoapTokenLength"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="UInt4"/> that indicates the length of the variable-length Token field.</param>
        public CoapTokenLength(UInt4 value)
        {
            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="UInt4"/> to <see cref="CoapTokenLength"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="CoapTokenLength"/> that is equivalent to the <paramref name="value"/>.
        /// </returns>
        public static explicit operator CoapTokenLength(UInt4 value) => new CoapTokenLength(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="CoapTokenLength"/> to <see cref="UInt4"/>.
        /// </summary>
        /// <param name="length">The length.</param>
        /// <returns>
        /// The <see cref="UInt4"/> that is equivalent to the <paramref name="length"/>.
        /// </returns>
        public static implicit operator UInt4(CoapTokenLength length) => length.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapTokenLength"/>s are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(CoapTokenLength left, CoapTokenLength right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapTokenLength"/>s are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(CoapTokenLength left, CoapTokenLength right) => !Equals(left, right);

        /// <summary>
        /// Indicates if the given <see cref="UInt2"/> is equal to the current <see cref="UInt2"/>.
        /// </summary>
        /// <param name="other">The other <see cref="UInt2"/>.</param>
        /// <returns><c>true</c> if the <paramref name="other"/> <see cref="UInt2"/> is equal to the current <see cref="UInt2"/>, otherwise <c>false</c>.</returns>
        public bool Equals(CoapTokenLength other)
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

            return this.Equals((CoapTokenLength)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}
