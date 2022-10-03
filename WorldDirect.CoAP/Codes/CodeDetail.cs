// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using System;
    using WorldDirect.CoAP.Common;

    /// <summary>
    /// Represent the detail subfield of a <see cref="Code"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="CodeDetail"/> represents a 5-bit unsigned integer and
    /// allows values from 0 to 31.
    /// </remarks>
    public class CodeDetail : IEquatable<CodeDetail>
    {
        private readonly UInt5 value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeDetail"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        public CodeDetail(UInt5 value)
        {
            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="UInt5"/> to <see cref="CodeDetail"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="CodeDetail"/> that is equivalent to the <see cref="UInt5"/> <paramref name="value"/>.
        /// </returns>
        public static explicit operator CodeDetail(UInt5 value) => new CodeDetail(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="CodeDetail"/> to <see cref="UInt5"/>.
        /// </summary>
        /// <param name="detail">The detail of the <see cref="Code"/>.</param>
        /// <returns>
        /// The <see cref="UInt5"/> that is equivalent to the <paramref name="detail"/> value.
        /// </returns>
        public static implicit operator UInt5(CodeDetail detail) => detail.value;

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CodeDetail"/>s are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(CodeDetail left, CodeDetail right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// <c>true</c> if both <see cref="CodeDetail"/> are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(CodeDetail left, CodeDetail right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.value:D2}";
        }

        /// <inheritdoc />
        public bool Equals(CodeDetail other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.value.Equals(other.value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((CodeDetail)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }
    }
}
