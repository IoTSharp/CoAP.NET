using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CoAP.Channel;
using CoAP.Codec;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;
using NetworkEndPoint = System.Net.EndPoint;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class MalformedDatagramTest
    {
        private static readonly IPEndPoint Remote = new IPEndPoint(IPAddress.Loopback, 56830);

        [TestCase("")]
        [TestCase("40")]
        [TestCase("4001")]
        [TestCase("400100")]
        [TestCase("41010001")]
        [TestCase("42010001aa")]
        [TestCase("48010001aabbcc")]
        [TestCase("49010001")]
        [TestCase("4f010001")]
        [TestCase("00010001")]
        [TestCase("80010001")]
        [TestCase("c0010001")]
        [TestCase("40010001d0")]
        [TestCase("40010001e000")]
        [TestCase("40010001bd")]
        [TestCase("40010001be00")]
        [TestCase("4001000111")]
        [TestCase("40010001f0")]
        [TestCase("400100010f")]
        [TestCase("40010001ff")]
        [TestCase("6000000100")]
        [TestCase("61000001aa")]
        [TestCase("60000001ffaa")]
        [TestCase("50000001")]
        [TestCase("70010001")]
        [TestCase("60010001")]
        [TestCase("70450001")]
        public void MalformedWireMessagesCannotDecode(String hex)
        {
            Assert.That(() => Spec.Decode(Convert.FromHexString(hex)), Throws.Exception);
        }

        [Test]
        public void EightByteTokenAndZeroLengthOptionDecodeWithoutPadding()
        {
            Request request = (Request)Spec.Decode(Convert.FromHexString("48010001000102030405060700"));

            Assert.That(request.Token, Is.EqualTo(Convert.FromHexString("0001020304050607")));
            Assert.That(request.GetFirstOption(OptionType.Reserved).RawValue, Is.Empty);
        }

        [Test]
        public void TruncatedFixedHeaderIsNotWellFormed()
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[] { 0x40, 0x01 });

            Assert.That(decoder.IsWellFormed, Is.False);
            Assert.That(() => decoder.DecodeRequest(), Throws.Exception);
        }

        [Test]
        public void TokenLengthGreaterThanEightIsNotWellFormed()
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[] { 0x49, 0x01, 0x00, 0x01 });

            Assert.That(decoder.IsWellFormed, Is.False);
            Assert.That(() => decoder.DecodeRequest(), Throws.Exception);
        }

        [Test]
        public void TruncatedOptionValueIsRejected()
        {
            // GET / with option delta 1 and length 1, but no value byte.
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[]
            {
                0x40, 0x01, 0x00, 0x01, 0x11
            });

            Assert.That(decoder.IsWellFormed, Is.True);
            Assert.That(() => decoder.DecodeRequest(), Throws.Exception);
        }

        [Test]
        public void TrailingPayloadMarkerIsRejected()
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[]
            {
                0x40, 0x01, 0x00, 0x01, 0xff
            });

            Assert.That(decoder.IsWellFormed, Is.True);
            Assert.That(() => decoder.DecodeRequest(), Throws.Exception);
        }

        [Test]
        public void InvalidVersionIsNotWellFormed()
        {
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[] { 0x00, 0x01, 0x00, 0x01 });

            Assert.That(decoder.IsWellFormed, Is.False);
        }

        [Test]
        public void EmptyMessageWithPayloadIsRejectedButValidEmptyMessageDecodes()
        {
            IMessageDecoder malformed = Spec.NewMessageDecoder(new Byte[]
            {
                0x60, 0x00, 0x00, 0x01, 0xff, 0x01
            });
            Assert.That(malformed.IsWellFormed, Is.True);
            Assert.That(() => malformed.DecodeEmptyMessage(), Throws.Exception);

            IMessageDecoder valid = Spec.NewMessageDecoder(new Byte[] { 0x60, 0x00, 0x00, 0x01 });
            Assert.That(valid.IsWellFormed, Is.True);
            Assert.That(valid.DecodeEmptyMessage(), Is.Not.Null);
        }

        [Test]
        public void UnknownElectiveOptionIsRetained()
        {
            // Option number 204 is unknown to this version and elective (even).
            IMessageDecoder decoder = Spec.NewMessageDecoder(new Byte[]
            {
                0x40, 0x01, 0x00, 0x01, 0xd1, 0xbf, 0x7f
            });

            Request request = decoder.DecodeRequest();
            Option option = request.GetFirstOption((OptionType)204);
            Assert.That(option, Is.Not.Null);
            Assert.That(option.RawValue, Is.EqualTo(new Byte[] { 0x7f }));
        }

        [Test]
        public void MalformedConfirmableRequestGetsOneReset()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                channel.Emit(new Byte[] { 0x40, 0x01, 0x00, 0x2a, 0x11 }, Remote);
            }

            Assert.That(channel.Sent, Has.Count.EqualTo(1));
            Assert.That(channel.Sent[0].Data, Is.EqualTo(new Byte[] { 0x70, 0x00, 0x00, 0x2a }));
            Assert.That(channel.Sent[0].EndPoint, Is.EqualTo(Remote));
        }

        [TestCase("4145002a")]
        [TestCase("4000002aff01")]
        [TestCase("4901002a")]
        [TestCase("4020002a")]
        public void IdentifiableMalformedConfirmableMessageGetsMatchingReset(String hex)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                channel.Emit(Convert.FromHexString(hex), Remote);
            }

            Assert.That(channel.Sent, Has.Count.EqualTo(1));
            Assert.That(channel.Sent[0].Data, Is.EqualTo(new Byte[] { 0x70, 0x00, 0x00, 0x2a }));
            Assert.That(channel.Sent[0].EndPoint, Is.EqualTo(Remote));
        }

        [Test]
        public void MalformedNonConfirmableRequestDoesNotGetReset()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                channel.Emit(new Byte[] { 0x50, 0x01, 0x00, 0x2a, 0x11 }, Remote);
            }

            Assert.That(channel.Sent, Is.Empty);
        }

        [Test]
        public void ShortHeadersAndUnsupportedVersionsAreSilentlyDropped()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                endpoint.ReceivingRequest += (sender, e) =>
                {
                    Assert.Fail("Malformed fixed header reached the request pipeline.");
                };
                channel.Emit(new Byte[] { 0x40, 0x01 }, Remote);
                channel.Emit(new Byte[] { 0x00, 0x01, 0x00, 0x01 }, Remote);
                channel.Emit(new Byte[] { 0x80, 0x01, 0x00, 0x01 }, Remote);
            }

            Assert.That(channel.Sent, Is.Empty);
        }

        [Test]
        public void CancelledMalformedMessageResetIsNotSent()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                endpoint.SendingEmptyMessage += (sender, e) => e.Message.IsCancelled = true;
                channel.Emit(new Byte[] { 0x40, 0x01, 0x00, 0x2a, 0x11 }, Remote);
            }

            Assert.That(channel.Sent, Is.Empty);
        }

        [Test]
        public void MalformedResponseAndEmptyMessageAreDroppedWithoutReset()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, CoapConfig.Default)
            {
                Executor = Executors.NoThreading
            })
            {
                endpoint.Start();
                // ACK response declares a one-byte token but ends at the header.
                channel.Emit(new Byte[] { 0x61, 0x45, 0x00, 0x01 }, Remote);
                // Empty ACK with a payload marker is malformed and must not be a ping.
                channel.Emit(new Byte[] { 0x60, 0x00, 0x00, 0x02, 0xff, 0x01 }, Remote);
                channel.Emit(new Byte[] { 0x50, 0x00, 0x00, 0x04 }, Remote);
            }

            Assert.That(channel.Sent, Is.Empty);
        }

        private sealed class RecordingChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived;

            public List<SentDatagram> Sent { get; } = new List<SentDatagram>();

            public NetworkEndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 0);

            public void Start()
            { }

            public void Stop()
            { }

            public void Send(Byte[] data, NetworkEndPoint endpoint)
            {
                Sent.Add(new SentDatagram(data.ToArray(), endpoint));
            }

            public void Emit(Byte[] data, NetworkEndPoint endpoint)
            {
                DataReceived?.Invoke(this, new DataReceivedEventArgs(data, endpoint));
            }

            public void Dispose()
            { }
        }

        private sealed class SentDatagram
        {
            public SentDatagram(Byte[] data, NetworkEndPoint endPoint)
            {
                Data = data;
                EndPoint = endPoint;
            }

            public Byte[] Data { get; }

            public NetworkEndPoint EndPoint { get; }
        }
    }
}
