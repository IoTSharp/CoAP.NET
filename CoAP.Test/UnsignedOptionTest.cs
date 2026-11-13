using System;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public class UnsignedOptionTest
    {
        [TestCase(OptionType.Size1)]
        [TestCase(OptionType.Size2)]
        public void LegacySizeGetterRetainsSignedFourByteMapping(OptionType type)
        {
            Request request = Request.NewGet();
            request.AddOption(Option.Create(type, new byte[] { 0x80, 0, 0, 0 }));

            Assert.That(type == OptionType.Size1 ? request.Size1 : request.Size2,
                Is.EqualTo(Int32.MinValue));
            Assert.That(request.GetFirstOption(type).LongValue, Is.EqualTo(0x80000000L));
        }

        [TestCase(OptionType.Size1)]
        [TestCase(OptionType.Size2)]
        public void LegacySizeGetterRejectsOverlongRawInteger(OptionType type)
        {
            Request request = Request.NewGet();
            request.AddOption(Option.Create(type, new byte[] { 1, 0, 0, 0, 0 }));

            Assert.Throws<OverflowException>(() =>
            {
                _ = type == OptionType.Size1 ? request.Size1 : request.Size2;
            });
        }

        [TestCase(OptionType.ContentFormat, 3)]
        [TestCase(OptionType.Accept, 3)]
        [TestCase(OptionType.UriPort, 3)]
        [TestCase(OptionType.Observe, 4)]
        [TestCase(OptionType.Block1, 4)]
        [TestCase(OptionType.Block2, 4)]
        [TestCase(OptionType.MaxAge, 5)]
        [TestCase(OptionType.Size1, 5)]
        [TestCase(OptionType.Size2, 5)]
        public void KnownIntegerGetterRejectsOverlongRawValue(OptionType type, int length)
        {
            Option option = Option.Create(type, new byte[length]);

            Assert.Throws<OverflowException>(() => _ = option.IntValue);
            Assert.Throws<OverflowException>(() => _ = option.LongValue);
            Assert.Throws<OverflowException>(() => _ = option.UIntValue);
        }

        [Test]
        public void UnsignedGetterCoversRfcRangeAndAbsentValue()
        {
            Request request = Request.NewGet();
            Assert.That(request.Size1Unsigned, Is.Null);
            Assert.That(request.Size2Unsigned, Is.Null);

            UInt32[] values = { 0U, 1U, 2147483647U, 0x80000000U, UInt32.MaxValue };
            foreach (UInt32 value in values)
            {
                request.Size1Unsigned = value;
                Assert.That(request.Size1Unsigned, Is.EqualTo(value));
                Assert.That(request.GetFirstOption(OptionType.Size1).RawValue.Length,
                    Is.EqualTo(value == 0U ? 0 : value <= 0xFFU ? 1 : value <= 0xFFFFU ? 2 : value <= 0xFFFFFFU ? 3 : 4));
            }

            request.Size1Unsigned = null;
            Assert.That(request.Size1Unsigned, Is.Null);
            request.Size2Unsigned = UInt32.MaxValue;
            Assert.That(request.Size2Unsigned, Is.EqualTo(UInt32.MaxValue));
        }

        [Test]
        public void UnsignedGetterAcceptsLegalLeadingZeros()
        {
            Option option = Option.Create(OptionType.Size1, new byte[] { 0, 0x80, 0, 0 });
            Assert.That(option.UIntValue, Is.EqualTo(0x800000U));
        }

        [Test]
        public void UnknownOptionRetainsGenericEightByteSignedMapping()
        {
            Option option = Option.Create((OptionType)65000, new byte[] { 0x98, 0x23, 0x74, 0x98, 0x37, 0x23, 0x98, 0x45 });
            Assert.That(option.LongValue, Is.EqualTo(unchecked((Int64)0x9823749837239845UL)));
            Assert.Throws<OverflowException>(() => _ = option.IntValue);
            Assert.Throws<OverflowException>(() => _ = option.UIntValue);
        }

        [Test]
        public void UnsignedSetterValidatesKnownOptionWithoutReplacingOldValue()
        {
            Option option = Option.Create(OptionType.ContentFormat, 50U);
            Assert.Throws<OverflowException>(() => option.UIntValue = 65536U);
            Assert.That(option.UIntValue, Is.EqualTo(50U));
        }

        [Test]
        public void ExistingCreateOverloadsRetainSignedWirePatterns()
        {
            Assert.That(Option.Create(OptionType.Size1, -1).UIntValue, Is.EqualTo(UInt32.MaxValue));
            Assert.That(Option.Create(OptionType.Size1, 2147483648L).UIntValue, Is.EqualTo(0x80000000U));
            Assert.That(Option.Create(OptionType.Size1, UInt32.MaxValue).IntValue, Is.EqualTo(-1));
        }

        [Test]
        public void GenericIntegerGettersRejectUnrepresentableRawWidth()
        {
            Option option = Option.Create((OptionType)65000, new byte[5]);
            Assert.Throws<OverflowException>(() => _ = option.IntValue);

            option.RawValue = new byte[9];
            Assert.Throws<OverflowException>(() => _ = option.LongValue);
        }

        [Test]
        public void MaxAgeGetterRejectsFiveByteRawValue()
        {
            Response response = new Response(StatusCode.Content);
            response.AddOption(Option.Create(OptionType.MaxAge, new byte[] { 1, 0, 0, 0, 0 }));

            Assert.Throws<OverflowException>(() => _ = response.MaxAge);
        }

        [Test]
        public void IntegerValueGetterRejectsOverlongRawValue()
        {
            Option option = Option.Create(OptionType.ContentFormat, new byte[] { 0, 0, 1 });
            Assert.Throws<OverflowException>(() => _ = option.Value);
        }
    }
}
