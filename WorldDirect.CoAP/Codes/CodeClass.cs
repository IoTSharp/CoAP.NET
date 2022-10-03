// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using System;
    using WorldDirect.CoAP.Common;

    /// <summary>
    /// Represents the class subfield of a <see cref="Code"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="CodeClass"/> represents a 3-bit unsigned integer
    /// and allows values from 0 to 7. The <see cref="CodeClass"/> can
    /// indicate a request (0), a success response (2), a client error
    /// response (4), or a server error response (5). Other values
    /// are reserved.
    /// </remarks>
    public class CodeClass : IEquatable<CodeClass>
    {
        private readonly UInt3 value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeClass"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        public CodeClass(UInt3 value)
        {
            this.value = value;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="UInt3"/> to <see cref="CodeClass"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="CodeClass"/> that is equivalent to the <see cref="UInt3"/> <paramref name="value"/>.
        /// </returns>
        public static explicit operator CodeClass(UInt3 value) => new CodeClass(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="CodeClass"/> to <see cref="UInt3"/>.
        /// </summary>
        /// <param name="class">The class.</param>
        /// <returns>
        /// The <see cref="UInt3"/> that is equivalent to the <see cref="CodeClass"/>.
        /// </returns>
        public static implicit operator UInt3(CodeClass @class) => @class.value;

        public static bool operator ==(CodeClass left, CodeClass right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CodeClass left, CodeClass right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.value.ToString();
        }

        public bool Equals(CodeClass other)
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

            return this.Equals((CodeClass)obj);
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }
    }
}
