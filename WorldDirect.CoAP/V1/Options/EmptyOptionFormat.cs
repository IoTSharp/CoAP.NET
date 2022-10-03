// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;

    /// <summary>
    /// Represents a <see cref="CoapOption"/> as empty. This means the value of this <see cref="CoapOption"/>
    /// is a zero-length sequence of bytes.
    /// </summary>
    /// <seealso cref="WorldDirect.CoAP.Messages.Options.CoapOption" />
    /// <seealso cref="System.IEquatable{WorldDirect.CoAP.Messages.Options.EmptyOptionFormat}" />
    public abstract class EmptyOptionFormat : CoapOption
    {
        private static readonly byte[] EmptyByteArray = new byte[0];

        /// <summary>
        /// Initializes a new instance of the <see cref="EmptyOptionFormat"/> class.
        /// </summary>
        protected EmptyOptionFormat(ushort number)
            : base(number, EmptyByteArray, 0)
        {
        }
    }
}
