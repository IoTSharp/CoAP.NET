using System;
using System.Linq;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class RegistryOptionTest
    {
        [TestCase(1)]
        [TestCase(11)]
        [TestCase(19)]
        [TestCase(205)]
        public void OddOptionNumbersAreCritical(Int32 number)
        {
            Assert.That(Option.IsCritical((OptionType)number), Is.True);
            Assert.That(Option.IsElective((OptionType)number), Is.False);
        }

        [TestCase(4)]
        [TestCase(204)]
        public void EvenOptionNumbersAreElective(Int32 number)
        {
            Assert.That(Option.IsCritical((OptionType)number), Is.False);
            Assert.That(Option.IsElective((OptionType)number), Is.True);
        }

        [Test]
        public void AddingRawOption19PreservesHeaderTokenAndOptionBytes()
        {
            Request request = new Request(Method.POST, true)
            {
                ID = 1,
                Token = new Byte[] { 0xaa, 0x55 }
            };

            // IANA assigns wire number 19 to Q-Block1. This test only
            // checks registry/raw-byte preservation, not Q-Block transfer.
            request.AddOption(Option.Create((OptionType)19, new Byte[] { 0x08 }));

            Assert.That(request.Token, Is.EqualTo(new Byte[] { 0xaa, 0x55 }));
            Assert.That(request.GetFirstOption((OptionType)19), Is.Not.Null);
            Assert.That(request.GetFirstOption((OptionType)19).RawValue, Is.EqualTo(new Byte[] { 0x08 }));
        }

        [Test]
        public void EncoderPreservesRawOption19()
        {
            Request request = new Request(Method.POST, true)
            {
                ID = 1,
                Token = new Byte[] { 0xaa, 0x55 }
            };
            request.AddOption(Option.Create((OptionType)19, new Byte[] { 0x08 }));

            // delta 19 is encoded as nibble 13 followed by 6.
            Assert.That(Spec.Encode(request), Is.EqualTo(Convert.FromHexString("42020001aa55d10608")));
        }

        [Test]
        public void DecoderPreservesRawOption19AndHeaderToken()
        {
            Request request = (Request)Spec.Decode(Convert.FromHexString("42020001aa55d10608"));

            Assert.That(request.Token, Is.EqualTo(new Byte[] { 0xaa, 0x55 }));
            Assert.That(request.GetFirstOption((OptionType)19), Is.Not.Null);
            Assert.That(request.GetFirstOption((OptionType)19).RawValue, Is.EqualTo(new Byte[] { 0x08 }));
        }

        [Test]
        public void RawUnknownCriticalOptionRoundTripsWithoutClaimingSupport()
        {
            Byte[] wire = Convert.FromHexString("42020001aa55d1c008");
            Request request = (Request)Spec.Decode(wire);

            Assert.That(request.Token, Is.EqualTo(new Byte[] { 0xaa, 0x55 }));
            Assert.That(request.GetFirstOption((OptionType)205).RawValue, Is.EqualTo(new Byte[] { 0x08 }));
            Assert.That(Spec.Encode(request), Is.EqualTo(wire));
        }

        [TestCase(19, "Q-Block1")]
        [TestCase(31, "Q-Block2")]
        public void CurrentRegistryUsesUnsignedIntegerQBlockMetadata(Int32 number, String name)
        {
            Option option = Option.Create((OptionType)number, 1);

            Assert.That(option.Name, Is.EqualTo(name));
            Assert.That(Option.GetFormatByType(option.Type), Is.EqualTo(OptionFormat.Integer));
            Assert.That(option.Value, Is.EqualTo(1));
            Assert.That(Option.Create((OptionType)number).IsDefault, Is.False);
        }

        [Test]
        public void EightByteHeaderTokenAndRepeatedRawOptionsRoundTrip()
        {
            Byte[] token = Convert.FromHexString("0102030405060708");
            Request request = new Request(Method.POST, true)
            {
                ID = 0x1234,
                Token = token
            };
            request.AddOption(Option.Create((OptionType)19, new Byte[] { 0x18 }));
            request.AddOption(Option.Create((OptionType)31, new Byte[] { 0x10 }));
            request.AddOption(Option.Create((OptionType)31, new Byte[] { 0x20 }));
            request.AddOption(Option.Create((OptionType)204, new Byte[] { 0xaa }));
            request.AddOption(Option.Create((OptionType)205, new Byte[] { 0xbb }));
            request.AddOption(Option.Create((OptionType)205, new Byte[] { 0xcc }));

            Byte[] bytes = Spec.Encode(request);
            Request decoded = (Request)Spec.Decode(bytes);

            Assert.That(request.Token, Is.EqualTo(token));
            Assert.That(decoded.Token, Is.EqualTo(token));
            Assert.That(decoded.GetOptions().Select(o => (Int32)o.Type), Is.EqualTo(new[] { 19, 31, 31, 204, 205, 205 }));
            Assert.That(decoded.GetOptions().Select(o => o.RawValue[0]), Is.EqualTo(new Byte[] { 0x18, 0x10, 0x20, 0xaa, 0xbb, 0xcc }));
            Assert.That(Spec.Encode(decoded), Is.EqualTo(bytes));
        }
    }
}
