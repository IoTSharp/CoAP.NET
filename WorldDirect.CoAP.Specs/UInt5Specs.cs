// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System;
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="UInt5"/>.
    /// </summary>
    public class UInt5Specs
    {
        private readonly UInt5 cut;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt5Specs"/> class.
        /// </summary>
        public UInt5Specs()
        {
            this.cut = default;
        }

        /// <summary>
        /// The default value of <see cref="UInt5"/> is 0.
        /// </summary>
        [Fact]
        public void DefaultTypeValueIsZero()
        {
            this.cut.Should().Be((UInt5)0);
        }

        /// <summary>
        /// The maximum value of <see cref="UInt5"/> is 31.
        /// </summary>
        [Fact]
        public void TheMaximumValueIs31()
        {
            UInt5.MaxValue.Should().Be((UInt5)31);
        }

        /// <summary>
        /// The minimum value of <see cref="UInt5"/> is 0.
        /// </summary>
        [Fact]
        public void TheMinimumValueIsZero()
        {
            UInt5.MinValue.Should().Be((UInt5)0);
        }

        /// <summary>
        /// <see cref="UInt5"/> throws <see cref="ArgumentOutOfRangeException"/> if value is greater than 31.
        /// </summary>
        [Fact]
        public void UInt4CanNotBeGreaterThan31()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => (UInt5)32);
        }
    }
}
