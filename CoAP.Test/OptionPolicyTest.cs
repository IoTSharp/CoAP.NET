using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class OptionPolicyTest
    {
        private static readonly IPEndPoint Peer = new IPEndPoint(IPAddress.Loopback, 56830);

        [TestCase(99, 0)]
        [TestCase(17, 3)]
        [TestCase(7, 3)]
        public void InvalidCriticalOptionInConfirmableRequestGetsBadOption(Int32 number, Int32 length)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Int32 delivered = 0;
                endpoint.ReceivingRequest += (sender, args) => { delivered++; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create((OptionType)number, new Byte[length]));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Zero);
                Assert.That(channel.Sent, Has.Count.EqualTo(1));
                Response error = (Response)Spec.Decode(channel.Sent[0]);
                Assert.That(error.StatusCode, Is.EqualTo(StatusCode.BadOption));
                Assert.That(error.Type, Is.EqualTo(MessageType.ACK));
                Assert.That(error.ID, Is.EqualTo(request.ID));
                Assert.That(error.Token, Is.EqualTo(request.Token));
                Assert.That(error.HasOption(OptionType.ContentFormat), Is.False);
            }
        }

        [Test]
        public void UnknownCriticalOptionInNonConfirmableRequestIsIgnored()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Int32 delivered = 0;
                endpoint.ReceivingRequest += (sender, args) => { delivered++; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.NON);
                request.AddOption(Option.Create((OptionType)99, Array.Empty<Byte>()));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Zero);
                Assert.That(channel.Sent, Is.Empty);
            }
        }

        [Test]
        public void InvalidElectiveContentFormatIsIgnoredWithoutIntegerTruncation()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ContentFormat, new Byte[] { 1, 0, 0, 0, 50 }));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.HasOption(OptionType.ContentFormat), Is.False);
                Assert.That(delivered.ContentFormat, Is.Not.EqualTo(50));
                Assert.That(channel.Sent, Is.Empty);
            }
        }

        [Test]
        public void DuplicateCriticalAcceptIsRejected()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => args.Message.Cancel();
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.Accept, new Byte[] { 50 }));
                request.AddOption(Option.Create(OptionType.Accept, new Byte[] { 0 }));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(channel.Sent, Has.Count.EqualTo(1));
                Assert.That(((Response)Spec.Decode(channel.Sent[0])).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [Test]
        public void DuplicateElectiveContentFormatKeepsOnlyFirstOccurrence()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ContentFormat, new Byte[] { 50 }));
                request.AddOption(Option.Create(OptionType.ContentFormat, new Byte[] { 0 }));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.GetOptions(OptionType.ContentFormat).Count(), Is.EqualTo(1));
                Assert.That(delivered.ContentFormat, Is.EqualTo(50));
            }
        }

        [Test]
        public void UnknownElectiveOptionRetainsItsRawValue()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create((OptionType)204, new Byte[] { 0xff, 0x80, 0x00 }));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.GetFirstOption((OptionType)204).RawValue,
                    Is.EqualTo(new Byte[] { 0xff, 0x80, 0x00 }));
            }
        }

        [TestCase(MessageType.CON, 1)]
        [TestCase(MessageType.ACK, 0)]
        [TestCase(MessageType.NON, 0)]
        public void UnknownCriticalResponseIsRejectedBeforeDelivery(MessageType type, Int32 expectedResets)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Int32 delivered = 0;
                endpoint.ReceivingResponse += (sender, args) => { delivered++; args.Message.Cancel(); };
                endpoint.Start();
                Response response = new Response(StatusCode.Content)
                {
                    ID = 43,
                    Type = type,
                    Token = new Byte[] { 1, 2, 3 }
                };
                response.AddOption(Option.Create((OptionType)99, Array.Empty<Byte>()));
                channel.Emit(Spec.Encode(response), Peer);

                Assert.That(delivered, Is.Zero);
                Assert.That(channel.Sent.Count, Is.EqualTo(expectedResets));
                if (expectedResets == 1)
                    Assert.That(channel.Sent[0], Is.EqualTo(new Byte[] { 0x70, 0x00, 0x00, 43 }));
            }
        }

        [TestCase(12, 3)]
        [TestCase(14, 5)]
        [TestCase(6, 4)]
        [TestCase(28, 5)]
        public void OversizedElectiveIntegerIsRemovedBeforeTypedAccess(Int32 number, Int32 length)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create((OptionType)number, new Byte[length]));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.HasOption((OptionType)number), Is.False);
            }
        }

        [Test]
        public void RepeatableUriPathRemainsAvailable()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.UriPath, "first"));
                request.AddOption(Option.Create(OptionType.UriPath, "second"));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.UriPaths, Is.EqualTo(new[] { "first", "second" }));
            }
        }

        [Test]
        public void CustomCriticalOptionRequiresExplicitPerEndpointRegistration()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.OptionRegistry.Register(new CoapOptionDefinition(99, "Custom", OptionFormat.Opaque,
                    1, 1, true, false, validator: (message, option) => option.RawValue[0] == 7));
                Int32 delivered = 0;
                endpoint.ReceivingRequest += (sender, args) => { delivered++; args.Message.Cancel(); };
                endpoint.Start();
                Request valid = NewRequest(MessageType.CON);
                valid.AddOption(Option.Create((OptionType)99, new Byte[] { 7 }));
                channel.Emit(Spec.Encode(valid), Peer);
                Request invalid = NewRequest(MessageType.CON);
                invalid.AddOption(Option.Create((OptionType)99, new Byte[] { 8 }));
                channel.Emit(Spec.Encode(invalid), Peer);

                Assert.That(delivered, Is.EqualTo(1));
                Assert.That(channel.Sent, Has.Count.EqualTo(1));
                Assert.That(((Response)Spec.Decode(channel.Sent[0])).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }

            RecordingChannel otherChannel = new RecordingChannel();
            using (CoAPEndPoint other = CreateEndpoint(otherChannel))
            {
                other.ReceivingRequest += (sender, args) => Assert.Fail("Registration leaked to another endpoint.");
                other.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create((OptionType)99, new Byte[] { 7 }));
                otherChannel.Emit(Spec.Encode(request), Peer);
                Assert.That(((Response)Spec.Decode(otherChannel.Sent.Single())).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProxySupportRequiresExplicitEndpointOptIn(Boolean supported)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.SupportsProxying = supported;
                Int32 delivered = 0;
                endpoint.ReceivingRequest += (sender, args) => { delivered++; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ProxyUri, "coap://example.test/value"));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.EqualTo(supported ? 1 : 0));
                Assert.That(channel.Sent.Count, Is.EqualTo(supported ? 0 : 1));
                if (!supported)
                    Assert.That(((Response)Spec.Decode(channel.Sent[0])).StatusCode, Is.EqualTo(StatusCode.ProxyingNotSupported));
            }
        }

        [TestCase(3)]
        [TestCase(7)]
        [TestCase(11)]
        [TestCase(15)]
        [TestCase(39)]
        public void ProxyUriCannotBeCombinedWithUriOrProxyScheme(Int32 conflictingNumber)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.SupportsProxying = true;
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Conflicting proxy options reached the host.");
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ProxyUri, "coap://example.test/value"));
                request.AddOption(Option.Create((OptionType)conflictingNumber,
                    conflictingNumber == 7 ? new Byte[] { 0x16, 0x33 } : System.Text.Encoding.UTF8.GetBytes("coap")));
                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(((Response)Spec.Decode(channel.Sent.Single())).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [TestCase("relative/path")]
        [TestCase("coap://example.test/value#fragment")]
        public void InvalidProxyUriGetsBadOption(String uri)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Invalid Proxy-Uri reached the host.");
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ProxyUri, uri));
                channel.Emit(Spec.Encode(request), Peer);
                Assert.That(((Response)Spec.Decode(channel.Sent.Single())).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public void ObserveRequestOnlyRecognizesRegisterAndDeregister(Byte value, Boolean retained)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.Observe, new Byte[] { value }));
                channel.Emit(Spec.Encode(request), Peer);
                Assert.That(delivered.HasOption(OptionType.Observe), Is.EqualTo(retained));
            }
        }

        [Test]
        public void ETagRepeatsInRequestButOnlyFirstResponseTagIsRetained()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request requestDelivered = null;
                Response responseDelivered = null;
                endpoint.ReceivingRequest += (sender, args) => { requestDelivered = args.Message; args.Message.Cancel(); };
                endpoint.ReceivingResponse += (sender, args) => { responseDelivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.ETag, new Byte[] { 1 }));
                request.AddOption(Option.Create(OptionType.ETag, new Byte[] { 2 }));
                channel.Emit(Spec.Encode(request), Peer);
                Response response = new Response(StatusCode.Content) { ID = 43, Type = MessageType.ACK };
                response.AddOption(Option.Create(OptionType.ETag, new Byte[] { 1 }));
                response.AddOption(Option.Create(OptionType.ETag, new Byte[] { 2 }));
                response.AddOption(Option.Create(OptionType.MaxAge, new Byte[] { 255, 255, 255, 255 }));
                channel.Emit(Spec.Encode(response), Peer);

                Assert.That(requestDelivered.GetOptions(OptionType.ETag).Count(), Is.EqualTo(2));
                Assert.That(responseDelivered.GetOptions(OptionType.ETag).Count(), Is.EqualTo(1));
                Assert.That(responseDelivered.GetFirstOption(OptionType.ETag).RawValue, Is.EqualTo(new Byte[] { 1 }));
                Assert.That(responseDelivered.GetFirstOption(OptionType.MaxAge).RawValue,
                    Is.EqualTo(new Byte[] { 255, 255, 255, 255 }));
            }
        }

        [Test]
        public void BuiltinDefinitionsCannotBeSilentlyOverwritten()
        {
            CoapOptionRegistry registry = new CoapOptionRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(new CoapOptionDefinition(
                17, "Accept override", OptionFormat.Integer, 0, 8, true, true)));
        }

        [TestCase(MessageType.CON)]
        [TestCase(MessageType.NON)]
        public void ValidProxySchemeWithoutProxySupportGetsFiveOhFive(MessageType type)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Proxy request reached a non-proxy host.");
                endpoint.Start();
                Request request = NewRequest(type);
                request.AddOption(Option.Create(OptionType.ProxyScheme, "coap"));
                request.AddOption(Option.Create(OptionType.UriHost, "example.test"));
                channel.Emit(Spec.Encode(request), Peer);

                Response response = (Response)Spec.Decode(channel.Sent.Single());
                Assert.That(response.StatusCode, Is.EqualTo(StatusCode.ProxyingNotSupported));
                Assert.That(response.Type, Is.EqualTo(type == MessageType.CON ? MessageType.ACK : MessageType.NON));
                Assert.That(response.Token, Is.EqualTo(request.Token));
            }
        }

        [Test]
        public void InvalidUtf8CriticalStringGetsBadOption()
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Invalid UTF-8 reached the host.");
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.UriHost, new Byte[] { 0xc0, 0xaf }));
                channel.Emit(Spec.Encode(request), Peer);
                Assert.That(((Response)Spec.Decode(channel.Sent.Single())).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void Size2RequestOnlyAllowsZero(Byte value, Boolean retained)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) => { delivered = args.Message; args.Message.Cancel(); };
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create(OptionType.Size2, new Byte[] { value }));
                channel.Emit(Spec.Encode(request), Peer);
                Assert.That(delivered.HasOption(OptionType.Size2), Is.EqualTo(retained));
            }
        }

        [TestCase(23)]
        [TestCase(27)]
        public void ReservedUdpBlockSizeInRequestGetsBadOption(Int32 number)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Reserved UDP SZX reached the stack.");
                endpoint.Start();
                Request request = NewRequest(MessageType.CON);
                request.AddOption(Option.Create((OptionType)number, new Byte[] { 7 }));
                channel.Emit(Spec.Encode(request), Peer);
                Assert.That(((Response)Spec.Decode(channel.Sent.Single())).StatusCode, Is.EqualTo(StatusCode.BadOption));
            }
        }

        [Test]
        public void InvalidBlockAcknowledgementDoesNotConsumeThePendingRequest()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = new CoapConfig { MaxMessageSize = 16, DefaultBlockSize = 16 };
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:56830/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
                client.SendAsync(request, fail: reason => Assert.Fail(reason.ToString()));
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response invalid = Response.CreateResponse(sent, StatusCode.Continue);
                invalid.Type = MessageType.ACK;
                invalid.ID = sent.ID;
                invalid.SetBlock1(7, true, 0);
                channel.Emit(Spec.Encode(invalid), Peer);

                Assert.That(channel.Sent.Count, Is.EqualTo(1));
                Assert.That(request.IsCancelled, Is.False);
                Response valid = Response.CreateResponse(sent, StatusCode.Continue);
                valid.Type = MessageType.ACK;
                valid.ID = sent.ID;
                valid.SetBlock1(0, true, 0);
                channel.Emit(Spec.Encode(valid), Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Assert.That(((Request)Spec.Decode(channel.Sent[1])).Block1.NUM, Is.EqualTo(1));
            }
        }

        private static Request NewRequest(MessageType type)
        {
            return new Request(Method.GET)
            {
                ID = 42,
                Type = type,
                Token = new Byte[] { 1, 2, 3 }
            };
        }

        private static CoAPEndPoint CreateEndpoint(RecordingChannel channel)
        {
            return new CoAPEndPoint(channel, new CoapConfig()) { Executor = Executors.NoThreading };
        }

        private sealed class RecordingChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived;
            public List<Byte[]> Sent { get; } = new List<Byte[]>();
            public System.Net.EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 10000);
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
            public void Send(Byte[] data, System.Net.EndPoint destination) { Sent.Add(data.ToArray()); }
            public void Emit(Byte[] data, System.Net.EndPoint source) { DataReceived?.Invoke(this, new DataReceivedEventArgs(data, source)); }
        }
    }
}
