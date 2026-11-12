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
    public sealed class OptionSemanticRegressionTest
    {
        private static readonly IPEndPoint Peer = new IPEndPoint(IPAddress.Loopback, 56830);
        private static readonly Byte[] HeaderToken = Convert.FromHexString("0102030405060708");

        [TestCase(Method.GET)]
        [TestCase(Method.POST)]
        [TestCase(Method.PUT)]
        public void EmptyIfNoneMatchIsValidForConditionalRequests(Method method)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Request delivered = null;
                endpoint.ReceivingRequest += (sender, args) =>
                {
                    delivered = args.Message;
                    delivered.Cancel();
                };
                endpoint.Start();
                Request request = NewRequest(method, MessageType.CON);
                request.IfNoneMatch = true;

                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.IfNoneMatch, Is.True);
                Assert.That(delivered.Token, Is.EqualTo(HeaderToken));
                Assert.That(channel.Sent, Is.Empty);
            }
        }

        [TestCase(".")]
        [TestCase("..")]
        public void InvalidElectiveLocationPathSegmentIsFiltered(String invalidSegment)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                Response delivered = null;
                endpoint.ReceivingResponse += (sender, args) =>
                {
                    delivered = args.Message;
                    delivered.Cancel();
                };
                endpoint.Start();
                Response response = new Response(StatusCode.Created)
                {
                    ID = 43,
                    Type = MessageType.ACK,
                    Token = HeaderToken
                };
                response.AddOption(Option.Create(OptionType.LocationPath, "first"));
                response.AddOption(Option.Create(OptionType.LocationPath, invalidSegment));
                response.AddOption(Option.Create(OptionType.LocationPath, "last"));
                response.AddOption(Option.Create(OptionType.LocationQuery, "created=true"));

                channel.Emit(Spec.Encode(response), Peer);

                Assert.That(delivered, Is.Not.Null);
                Assert.That(delivered.GetOptions(OptionType.LocationPath).Select(option => option.StringValue),
                    Is.EqualTo(new[] { "first", "last" }));
                Assert.That(delivered.LocationQueries, Is.EqualTo(new[] { "created=true" }));
                Assert.That(channel.Sent, Is.Empty);
            }
        }

        [TestCase(19, MessageType.CON)]
        [TestCase(19, MessageType.NON)]
        [TestCase(31, MessageType.CON)]
        [TestCase(31, MessageType.NON)]
        public void UnimplementedQBlockOptionsAreRejectedWithoutReplacingHeaderToken(Int32 number, MessageType type)
        {
            RecordingChannel channel = new RecordingChannel();
            using (CoAPEndPoint endpoint = CreateEndpoint(channel))
            {
                endpoint.ReceivingRequest += (sender, args) => Assert.Fail("Unimplemented Q-Block reached the host.");
                endpoint.Start();
                Request request = NewRequest(Method.POST, type);
                request.AddOption(Option.Create((OptionType)number, new Byte[] { 0x08 }));

                channel.Emit(Spec.Encode(request), Peer);

                Assert.That(request.Token, Is.EqualTo(HeaderToken));
                if (type == MessageType.CON)
                {
                    Response error = (Response)Spec.Decode(channel.Sent.Single());
                    Assert.That(error.StatusCode, Is.EqualTo(StatusCode.BadOption));
                    Assert.That(error.Type, Is.EqualTo(MessageType.ACK));
                    Assert.That(error.ID, Is.EqualTo(request.ID));
                    Assert.That(error.Token, Is.EqualTo(HeaderToken));
                }
                else
                {
                    Assert.That(channel.Sent, Is.Empty);
                }
            }
        }

        private static Request NewRequest(Method method, MessageType type)
        {
            return new Request(method)
            {
                ID = 42,
                Type = type,
                Token = HeaderToken
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
