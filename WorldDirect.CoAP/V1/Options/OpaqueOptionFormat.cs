// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;
    using System.Linq;
    using Common.Extensions;

    /// <summary>
    /// Represents a <see cref="CoapOption"/> as a opaque sequence of bytes. This means the value of that <see cref="CoapOption"/>
    /// is a opaque sequence of bytes.
    /// </summary>
    /// <seealso cref="WorldDirect.CoAP.Messages.Options.CoapOption" />
    /// <seealso cref="System.IEquatable{WorldDirect.CoAP.Messages.Options.OpaqueOptionFormat}" />
    public abstract class OpaqueOptionFormat : CoapOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpaqueOptionFormat"/> class.
        /// </summary>
        /// <param name="value">The byte array (in network byte order) that represents the value of that option.</param>
        /// <remarks>
        /// If the system's computer architecture is in little endian order, the <paramref name="value" /> will be
        /// reversed, because the <paramref name="value" /> is in network byte order (big endian order).
        /// </remarks>
        protected OpaqueOptionFormat(ushort number, byte[] value, uint lowerLimit, uint upperLimit)
            : base(number, value, lowerLimit, upperLimit)
        {
        }

        protected OpaqueOptionFormat(ushort number, byte[] value, uint lowerLimit)
            : base(number, value, lowerLimit)
        {
        }

        public override string ToString() => $"{base.ToString()}: {this.RawValue.ToString(' ')}";
    }
}
