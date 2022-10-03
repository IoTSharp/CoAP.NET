// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using WorldDirect.CoAP.V1.Messages;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="CoapVersion"/>.
    /// </summary>
    public class CoapVersionSpecs
    {
        /// <summary>
        /// The default version for RFC 7252 is 1 (01).
        /// </summary>
        [Fact]
        public void TheDefaultVersionForRfc7252IsBinaryValue01()
        {
            var v1 = CoapVersion.V1;
            ((UInt2)v1).Should().Be((UInt2)1);
        }
    }
}
