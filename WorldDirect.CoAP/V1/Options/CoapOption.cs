namespace WorldDirect.CoAP.V1.Options
{
    using System;
    using System.Linq;

    /// <summary>
    /// Represents a option specified by RFC 7252.
    /// </summary>
    public abstract class CoapOption : IEquatable<CoapOption>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoapOption"/> class.
        /// </summary>
        /// <param name="value">The byte array (in network byte order) that represents the value of that option.</param>
        /// <remarks>
        /// If the system's computer architecture is in little endian order, the <paramref name="value"/>
        /// will be reversed, because the <paramref name="value"/> is expected to be in network byte order (big endian order).
        /// </remarks>
        protected CoapOption(ushort number, byte[] value,  uint lowerLimit, uint upperLimit)
        {
            this.Number = number;
            this.Name = this.Dasherize();

            if (lowerLimit > upperLimit)
            {
                throw new ArgumentException($"The lower limit of {lowerLimit} can not be greater than the upper limit of {upperLimit}.");
            }

            this.LowerLimit = lowerLimit;
            this.UpperLimit = upperLimit;

            if (value.Length < lowerLimit || value.Length > upperLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The length of the value is out of range [{this.LowerLimit} - {this.UpperLimit} bytes].");
            }

            this.RawValue = value;
        }

        protected CoapOption(ushort number, byte[] value, uint lowerLimit)
            : this(number, value, lowerLimit, lowerLimit)
        {
        }

        /// <summary>
        /// Gets the number of that <see cref="CoapOption"/>.
        /// </summary>
        /// <value>
        /// The number.
        /// </value>
        public ushort Number { get; }

        public string Name { get; }

        /// <summary>
        /// Gets the raw value of that <see cref="CoapOption"/> in the system's computer architecture.
        /// </summary>
        /// <value>
        /// The raw value.
        /// </value>
        public byte[] RawValue { get; }

        public uint UpperLimit { get; }

        public uint LowerLimit { get; }

        public override string ToString() => $"{this.Name} ({this.Number})";

        public bool Equals(CoapOption other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Number == other.Number && this.Name == other.Name && this.RawValue.SequenceEqual(other.RawValue);
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

            return Equals((CoapOption) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Number, Name, RawValue);
        }

        public static bool operator ==(CoapOption left, CoapOption right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CoapOption left, CoapOption right)
        {
            return !Equals(left, right);
        }
    }
}
