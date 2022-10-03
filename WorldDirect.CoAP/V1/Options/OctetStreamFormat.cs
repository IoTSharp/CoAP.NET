// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using WorldDirect.CoAP.V1.Messages;

    /// <summary>
    /// Represents the <see cref="ContentFormat"/> of a <see cref="CoapMessage"/> as a octet stream.
    /// </summary>
    /// <seealso cref="ContentFormat" />
    public sealed class OctetStreamFormat : ContentFormat
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OctetStreamFormat"/> class.
        /// </summary>
        public OctetStreamFormat()
            : base(42, CoapMediaTypeNames.Application.OctetStream)
        {
        }
    }
}
