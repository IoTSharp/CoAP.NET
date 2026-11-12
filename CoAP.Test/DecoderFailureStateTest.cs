using System;
using System.IO;
using CoAP.Codec;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class DecoderFailureStateTest
    {
        [TestCase("4001000111", true)]
        [TestCase("4045000111", false)]
        public void TypedDecodeFailureCannotBeRetriedThroughAnyEntryPoint(String hex, Boolean isRequest)
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(Convert.FromHexString(hex));

            if (isRequest)
                Assert.Throws<EndOfStreamException>(() => decoder.DecodeRequest());
            else
                Assert.Throws<EndOfStreamException>(() => decoder.DecodeResponse());

            AssertFailedAcrossEntryPoints(decoder);
        }

        [TestCase("4001000111")]
        [TestCase("4045000111")]
        public void GenericDecodeFailureCannotBeRetriedThroughTypedEntryPoints(String hex)
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(Convert.FromHexString(hex));

            Assert.Throws<EndOfStreamException>(() => decoder.Decode());

            AssertFailedAcrossEntryPoints(decoder);
        }

        private static void AssertFailedAcrossEntryPoints(IMessageDecoder decoder)
        {
            Assert.Throws<FormatException>(() => decoder.Decode());
            Assert.Throws<FormatException>(() => decoder.DecodeRequest());
            Assert.Throws<FormatException>(() => decoder.DecodeResponse());
            Assert.Throws<FormatException>(() => decoder.DecodeEmptyMessage());
        }
    }
}
