// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Messages
{
    using WorldDirect.CoAP.Common;

    /// <summary>
    /// Represents the first two bits unsigned integer of a CoAP message.
    /// It indicates the CoAP version number.
    /// </summary>
    public readonly struct CoapVersion
    {
        /// <summary>
        /// The default value for the <see cref="CoapVersion"/> specified in RFC 7252.
        /// </summary>
        public static readonly CoapVersion V1 = new CoapVersion((UInt2)1);

        private readonly UInt2 value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoapVersion"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="UInt2"/> value that represents the version number of a CoAP Message.</param>
        public CoapVersion(UInt2 value)
        {
            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="UInt2"/> to <see cref="CoapVersion"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="CoapVersion"/> that is equivalent to the given <see cref="UInt2"/> value.
        /// </returns>
        public static explicit operator CoapVersion(UInt2 value) => new CoapVersion(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="CoapVersion"/> to <see cref="UInt2"/>.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns>
        /// A <see cref="UInt2"/> that is equivalent to the given <see cref="CoapVersion"/> value.
        /// </returns>
        public static implicit operator UInt2(CoapVersion version) => version.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapVersion"/>s are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(CoapVersion left, CoapVersion right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapVersion"/>s are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(CoapVersion left, CoapVersion right) => !Equals(left, right);

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

            return this.Equals((CoapVersion)obj);
        }

        /// <summary>
        /// Indicates if the specified <see cref="CoapVersion"/> is equal to the current <see cref="CoapVersion"/>.
        /// </summary>
        /// <param name="other">The other <see cref="CoapVersion"/>.</param>
        /// <returns><c>true</c> if the specified <paramref name="other"/> <see cref="CoapVersion"/> is equal to the current <see cref="CoapVersion"/>.</returns>
        public bool Equals(CoapVersion other)
        {
            return this.value == other.value;
        }

        /// <inheritdoc />
        public override int GetHashCode() => this.value.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => $"V{this.value}";
    }
}
