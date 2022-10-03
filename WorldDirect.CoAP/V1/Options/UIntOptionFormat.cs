// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;
    using System.Buffers.Binary;
    using System.Linq;
    using Common.Extensions;

    /// <summary>
    /// Represents a <see cref="CoapOption"/> as in <see cref="uint"/> format. This means the value
    /// of the <see cref="CoapOption"/> is a <see cref="uint"/>.
    /// </summary>
    public abstract class UIntOptionFormat : CoapOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UIntOptionFormat"/> class.
        /// </summary>
        /// <param name="value">The byte array (in network byte order) that represents the value of that option.</param>
        /// <remarks>
        /// If the system's computer architecture is in little endian order, the <paramref name="value"/> will be
        /// reversed, because the <paramref name="value"/> is in network byte order (big endian order).
        /// </remarks>
        protected UIntOptionFormat(ushort number, uint value, uint lowerLimit, uint upperLimit)
            : base(number, BitConverter.GetBytes(value).RemoveLeadingZeros(), lowerLimit, upperLimit)
        {
        }

        protected UIntOptionFormat(ushort number, uint value, uint lowerLimit)
            : this(number, value, lowerLimit, lowerLimit)
        {
        }

        public uint Value => BinaryPrimitives.ReadUInt32BigEndian(this.RawValue.AsSpan().Align(32));

        public override string ToString() => $"{base.ToString()}: {this.Value:D}";
    }
}
