// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using WorldDirect.CoAP.Common;
    using FluentAssertions;
    using WorldDirect.CoAP.V1.Messages;
    using Xunit;

    /// <summary>
    /// Provides all specs for <see cref="CoapType"/>.
    /// </summary>
    public class CoapTypeSpecs
    {
        /// <summary>
        /// The value of confirmable type is 0.
        /// </summary>
        [Fact]
        public void ConfirmableTypeIsValueZero()
        {
            var confirmable = CoapType.Confirmable;
            ((UInt2)confirmable).Should().Be((UInt2)0);
        }

        /// <summary>
        /// The value of non confirmable type is 1.
        /// </summary>
        [Fact]
        public void NonConfirmableTypeIsValueOne()
        {
            var nonConfirmable = CoapType.NonConfirmable;
            ((UInt2)nonConfirmable).Should().Be((UInt2)1);
        }

        /// <summary>
        /// The value of acknowledgement type is 2.
        /// </summary>
        [Fact]
        public void AcknowledgementTypeIsValueTwo()
        {
            var acknowledgement = CoapType.Acknowledgement;
            ((UInt2)acknowledgement).Should().Be((UInt2)2);
        }

        /// <summary>
        /// The value of reset type is 3.
        /// </summary>
        [Fact]
        public void RestTypeIsValueThree()
        {
            var reset = CoapType.Reset;
            ((UInt2)reset).Should().Be((UInt2)3);
        }
    }
}
