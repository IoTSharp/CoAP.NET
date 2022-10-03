// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System;
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="UInt4"/>.
    /// </summary>
    public class UInt4Specs
    {
        private readonly UInt4 cut;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt4Specs"/> class.
        /// </summary>
        public UInt4Specs()
        {
            this.cut = default;
        }

        /// <summary>
        /// The default value of <see cref="UInt4"/> is 0.
        /// </summary>
        [Fact]
        public void DefaultTypeValueIsZero()
        {
            this.cut.Should().Be((UInt4)0);
        }

        /// <summary>
        /// The maximum value of <see cref="UInt4"/> is 15.
        /// </summary>
        [Fact]
        public void TheMaximumValueIs15()
        {
            UInt4.MaxValue.Should().Be((UInt4)15);
        }

        /// <summary>
        /// The minimum value of <see cref="UInt4"/> is 0.
        /// </summary>
        [Fact]
        public void TheMinimumValueIsZero()
        {
            UInt4.MinValue.Should().Be((UInt4)0);
        }

        /// <summary>
        /// <see cref="UInt4"/> throws a <see cref="ArgumentOutOfRangeException"/> if the value is greater than 15.
        /// </summary>
        [Fact]
        public void UInt4CanNotBeGreaterThan15()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => (UInt4)16);
        }
    }
}
