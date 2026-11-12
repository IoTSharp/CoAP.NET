#if COAPALL || COAP03 || COAP08 || COAP12
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class LegacyCodecOptionTest
    {
        public static IEnumerable<TestCaseData> TokenOptionCodecs
        {
            get
            {
#if COAPALL || COAP03
                yield return new TestCaseData(Spec.Draft03).SetName("LegacyDraft03TokenRoundTrip");
#endif
#if COAPALL || COAP08
                yield return new TestCaseData(Spec.Draft08).SetName("LegacyDraft08TokenRoundTrip");
#endif
#if COAPALL || COAP12
                yield return new TestCaseData(Spec.Draft12).SetName("LegacyDraft12TokenRoundTrip");
#endif
            }
        }

        [TestCaseSource(nameof(TokenOptionCodecs))]
        public void ExplicitLegacyCodecTranslatesTokenWithoutChangingModernCodec(ISpec legacy)
        {
            Byte[] token = Convert.FromHexString("0102030405060708");
            Request request = new Request(Method.GET, true)
            {
                ID = 0x1234,
                Token = token
            };
            request.UriPath = "/x";

            Message decoded = legacy.Decode(legacy.Encode(request));

            Assert.That(decoded.Token, Is.EqualTo(token));
            Assert.That(decoded.HasOption((OptionType)19), Is.False);
            Assert.That(Spec.Default, Is.SameAs(Spec.RFC7252));

            Request modern = new Request(Method.POST, true) { ID = 1, Token = token };
            modern.AddOption(Option.Create((OptionType)19, new Byte[] { 0x08 }));
            Message modernDecoded = Spec.Decode(Spec.Encode(modern));

            Assert.That(modernDecoded.Token, Is.EqualTo(token));
            Assert.That(modernDecoded.GetFirstOption((OptionType)19).RawValue, Is.EqualTo(new Byte[] { 0x08 }));
        }

        [TestCaseSource(nameof(TokenOptionCodecs))]
        public void LegacyCodecRejectsAmbiguousModernOption19(ISpec legacy)
        {
            Request message = new Request(Method.POST, true) { ID = 1, Token = new Byte[] { 0xaa } };
            message.AddOption(Option.Create((OptionType)19, new Byte[] { 0x08 }));

            Assert.That(() => legacy.Encode(message), Throws.InvalidOperationException);
            Assert.That(message.Token, Is.EqualTo(new Byte[] { 0xaa }));
        }
    }
}
#endif
