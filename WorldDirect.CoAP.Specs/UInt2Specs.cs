// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System;
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="UInt2"/>.
    /// </summary>
    public class UInt2Specs
    {
        private readonly UInt2 cut;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt2Specs"/> class.
        /// </summary>
        public UInt2Specs()
        {
            this.cut = default(UInt2);
        }

        /// <summary>
        /// The default value of <see cref="UInt2"/> is 0.
        /// </summary>
        [Fact]
        public void TheTypeDefaultValueIsZero()
        {
            this.cut.Should().Be((UInt2)0);
        }

        /// <summary>
        /// The maximum value of <see cref="UInt2"/> is 3.
        /// </summary>
        [Fact]
        public void TheMaximumValueIsThree()
        {
            UInt2.MaxValue.Should().Be((UInt2)3);
        }

        /// <summary>
        /// The minimum value of <see cref="UInt2"/> is 0.
        /// </summary>
        [Fact]
        public void TheMinimumValueIsZero()
        {
            UInt2.MinValue.Should().Be((UInt2)0);
        }

        /// <summary>
        /// <see cref="UInt2"/> throws a <see cref="ArgumentOutOfRangeException"/> if the value is greater than 3.
        /// </summary>
        [Fact]
        public void UInt2CanNotBeGreaterThanThree()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => (UInt2)4);
        }
    }
}
