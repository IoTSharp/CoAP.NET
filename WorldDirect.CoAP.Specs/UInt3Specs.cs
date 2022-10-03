// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System;
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="UInt3"/>.
    /// </summary>
    public class UInt3Specs
    {
        private readonly UInt3 cut;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt3Specs"/> class.
        /// </summary>
        public UInt3Specs()
        {
            this.cut = default;
        }

        /// <summary>
        /// The default value for <see cref="UInt2"/> is 0.
        /// </summary>
        [Fact]
        public void DefaultTypeValueIsZero()
        {
            this.cut.Should().Be((UInt3)0);
        }

        /// <summary>
        /// The maximum value for <see cref="UInt2"/> is 7.
        /// </summary>
        [Fact]
        public void TheMaximumValueIsSeven()
        {
            UInt3.MaxValue.Should().Be((UInt3)7);
        }

        /// <summary>
        /// The minimum value for <see cref="UInt2"/> is 0.
        /// </summary>
        [Fact]
        public void TheMinimumValueIsZero()
        {
            UInt3.MinValue.Should().Be((UInt3)0);
        }

        /// <summary>
        /// <see cref="UInt3"/> throws a <see cref="ArgumentOutOfRangeException"/> if the value is greater than 7.
        /// </summary>
        [Fact]
        public void UInt3CanNotBeGreaterThanSeven()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => (UInt3)8);
        }
    }
}
