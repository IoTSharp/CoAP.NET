// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Codes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.Codes.MethodCodes;

    /// <summary>
    /// Provides functionality to add and remove <see cref="CoapCode"/>s from the <see cref="CodeRegistry"/>.
    /// This registry stores all registered <see cref="CoapCode"/> for the whole application.
    /// </summary>
    public class CodeRegistry : Registry<CoapCode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeRegistry"/> class.
        /// </summary>
        /// <param name="codes">The set of <see cref="CoapCode"/>s that should represent the new created <see cref="CodeRegistry"/>.</param>
        /// <remarks>
        /// This will overwrite the default <see cref="CoapCode"/>s specifies by RFC 7252.
        /// </remarks>
        public CodeRegistry(IEnumerable<CoapCode> codes)
            : base(codes)
        {
        }

        /// <summary>
        /// Gets the <see cref="CoapCode"/> with the given <paramref name="class"/> and <paramref name="detail"/>.
        /// </summary>
        /// <param name="class">The <see cref="CodeClass"/> of the required <see cref="CoapCode"/>.</param>
        /// <param name="detail">The <see cref="CodeDetail"/> of the required <see cref="CoapCode"/>.</param>
        /// <returns>The <see cref="CoapCode"/> that matches the given <paramref name="class"/> and <see cref="detail"/>.</returns>
        /// <exception cref="ArgumentException">Throws if none <see cref="CoapCode"/> exists in the <see cref="CodeRegistry"/> with the given <paramref name="class"/> and <paramref name="detail"/>.</exception>
        public CoapCode Get(CodeClass @class, CodeDetail detail)
        {
            if (!this.Exists(@class, detail))
            {
                return new UnknownCode(@class, detail);
            }

            var code = this.Elements.Single(c => c.Class.Equals(@class) && c.Detail.Equals(detail));
            return code;
        }

        private bool Exists(CodeClass @class, CodeDetail detail)
        {
            return this.Elements.Any(c => c.Class.Equals(@class) && c.Detail.Equals(detail));
        }
    }
}
