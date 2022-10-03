// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Messages
{
    using WorldDirect.CoAP.Common;

    /// <summary>
    /// Represents a two bit unsigned integer of a CoAP message and appears after the <see cref="CoapVersion"/>. It indicates
    /// if this message is of type Confirmable (0), Non-Confirmable(1), Acknowledgement (2), or Reset (3).
    /// </summary>
    /// <remarks>
    /// The semantics of these message <see cref="CoapType"/>s are defined in Section 4 of RFC 7252.
    /// </remarks>
    public readonly struct CoapType
    {
        /// <summary>
        /// The <see cref="CoapType"/> that represents a CoAP message as Confirmable (0).
        /// </summary>
        /// <remarks>
        /// The value is specified by RFC 7252.
        /// </remarks>
        public static readonly CoapType Confirmable = new CoapType((UInt2)0);

        /// <summary>
        /// The <see cref="CoapType"/> that represents a CoAP message as Non-Confirmable (1).
        /// </summary>
        /// <remarks>
        /// The value is specified by RFC 7252.
        /// </remarks>
        public static readonly CoapType NonConfirmable = new CoapType((UInt2)1);

        /// <summary>
        /// The <see cref="CoapType"/> that represents a CoAP message as Acknowledgment (2).
        /// </summary>
        /// <remarks>
        /// The value is specified by RFC 7252.
        /// </remarks>
        public static readonly CoapType Acknowledgement = new CoapType((UInt2)2);

        /// <summary>
        /// The <see cref="CoapType"/> that represents a CoAP message as Reset (3).
        /// </summary>
        /// <remarks>
        /// The value is specified by RFC 7252.
        /// </remarks>
        public static readonly CoapType Reset = new CoapType((UInt2)3);

        private readonly UInt2 value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoapType"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="UInt2"/> value that represents the coapType of a CoAP message.</param>
        public CoapType(UInt2 value)
        {
            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="UInt2"/> to <see cref="CoapType"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="CoapType"/> that is equivalent to the <paramref name="value"/>.
        /// </returns>
        public static explicit operator CoapType(UInt2 value) => new CoapType(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="CoapType"/> to <see cref="UInt2"/>.
        /// </summary>
        /// <param name="type">The <see cref="CoapType"/>.</param>
        /// <returns>
        /// The <see cref="UInt2"/> that is equivalent to the <paramref name="coapType"/>.
        /// </returns>
        public static implicit operator UInt2(CoapType type) => type.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapType"/>s are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(CoapType left, CoapType right) => Equals(left, right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CoapType"/>s are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(CoapType left, CoapType right) => !Equals(left, right);

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

            return this.Equals((CoapType)obj);
        }

        /// <summary>
        /// Indicates if the specified <see cref="CoapType"/> is equal to the current <see cref="CoapType"/>.
        /// </summary>
        /// <param name="other">The other <see cref="CoapType"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="CoapType"/> is equal to the current <see cref="CoapType"/>.</returns>
        public bool Equals(CoapType other)
        {
            return this.value == other.value;
        }

        /// <inheritdoc />
        public override int GetHashCode() => this.value.GetHashCode();

        /// <inheritdoc />
        public override string ToString()
        {
            if (this.value == Confirmable.value)
            {
                return nameof(Confirmable);
            }

            if (this.value == NonConfirmable.value)
            {
                return nameof(NonConfirmable);
            }

            if (this.value == Acknowledgement.value)
            {
                return nameof(Acknowledgement);
            }

            if (this.value == Reset.value)
            {
                return nameof(Reset);
            }

            return this.value.ToString();
        }
    }
}
