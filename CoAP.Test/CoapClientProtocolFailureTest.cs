using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;
using NetworkEndPoint = System.Net.EndPoint;

namespace CoAP
{
    [TestFixture]
    public sealed class CoapClientProtocolFailureTest
    {
        private static readonly IPEndPoint Peer = new IPEndPoint(IPAddress.Loopback, 5683);

        [Test]
        public void InvalidAtomicBlock1Response_CancelsAndNotifiesAsyncCaller()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
                Int32 successes = 0;
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                List<CoapClient.FailReason> errors = new List<CoapClient.FailReason>();
                client.Error += (sender, e) => errors.Add(e.Reason);
                client.SendAsync(request, response => successes++, reason => failures.Add(reason));

                Request first = (Request)Spec.Decode(channel.Sent[0]);
                Response invalid = Response.CreateResponse(first, StatusCode.Changed);
                invalid.Type = MessageType.ACK;
                invalid.ID = first.ID;
                invalid.SetBlock1(0, true, 0);
                channel.Emit(Spec.Encode(invalid), Peer);

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(request.WaitForResponse(0), Is.Null);
                Assert.That(channel.Sent.Count, Is.EqualTo(1), "An invalid atomic response must not advance the upload.");
                Assert.That(successes, Is.Zero, "A partial upload must never be reported as success.");
                Assert.That(failures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
                Assert.That(errors, Is.EqualTo(failures));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WrongPeerOrToken_CannotAdvanceBlock1ButCorrectResponseCan(Boolean wrongPeer)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
                client.SendAsync(request);
                Request first = (Request)Spec.Decode(channel.Sent[0]);
                Response invalid = Response.CreateResponse(first, StatusCode.Continue);
                invalid.Type = MessageType.ACK;
                invalid.ID = first.ID;
                invalid.SetBlock1(0, true, 0);
                if (!wrongPeer)
                {
                    invalid.Token = first.Token.ToArray();
                    invalid.Token[0] ^= 0xff;
                }

                channel.Emit(Spec.Encode(invalid), wrongPeer ? new IPEndPoint(Peer.Address, Peer.Port + 1) : Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(1));

                Response valid = Response.CreateResponse(first, StatusCode.Continue);
                valid.Type = MessageType.ACK;
                valid.ID = first.ID;
                valid.SetBlock1(0, true, 0);
                channel.Emit(Spec.Encode(valid), Peer);

                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Request second = (Request)Spec.Decode(channel.Sent[1]);
                Assert.That(second.Block1.NUM, Is.EqualTo(1));
                Assert.That(second.Payload, Is.EqualTo(request.Payload.Skip(16).Take(16).ToArray()));
                request.Cancel();
            }
        }

        [Test]
        public void SendAsync_ExplicitCancel_ReportsAppendedFailureReason()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/value"), config) { EndPoint = endpoint };
                Request request = Request.NewGet();
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                client.SendAsync(request, response => Assert.Fail("Cancelled request delivered a response."), reason => failures.Add(reason));
                request.Cancel();
                request.Cancel();

                Assert.That(failures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
                Assert.That((Int32)CoapClient.FailReason.Rejected, Is.EqualTo(0));
                Assert.That((Int32)CoapClient.FailReason.TimedOut, Is.EqualTo(1));
                Assert.That((Int32)CoapClient.FailReason.Cancelled, Is.EqualTo(2));
            }
        }

        private static CoapConfig CreateConfig()
        {
            return new CoapConfig { MaxMessageSize = 16, DefaultBlockSize = 16 };
        }

        private sealed class RecordingChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived;
            public List<Byte[]> Sent { get; } = new List<Byte[]>();
            public NetworkEndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 10000);
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
            public void Send(Byte[] data, NetworkEndPoint destination) { Sent.Add(data.ToArray()); }
            public void Emit(Byte[] data, NetworkEndPoint source) { DataReceived?.Invoke(this, new DataReceivedEventArgs(data, source)); }
        }
    }
}
