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


        [TestCase(false)]
        [TestCase(true)]
        public void PerBlockResponseWithBlock2_ContinuesUploadBeforeFinalDelivery(Boolean moreResponseBlocks)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(Enumerable.Range(0, 48).Select(value => (Byte)value).ToArray(), MediaType.ApplicationOctetStream);
                List<Response> responses = new List<Response>();
                client.SendAsync(request, response => responses.Add(response), reason => Assert.Fail(reason.ToString()));

                Request first = (Request)Spec.Decode(channel.Sent[0]);
                Response partial = CreateAck(first, StatusCode.Changed);
                partial.SetBlock1(0, false, 0);
                partial.SetBlock2(0, moreResponseBlocks, 0);
                partial.SetPayload(new Byte[moreResponseBlocks ? 16 : 1], MediaType.ApplicationOctetStream);
                channel.Emit(Spec.Encode(partial), Peer);

                Assert.That(responses, Is.Empty, "A per-block response is not completion of the entire upload.");
                Assert.That(channel.Sent.Count, Is.EqualTo(2), "Only one next request may be outstanding.");
                Request second = (Request)Spec.Decode(channel.Sent[1]);
                Assert.That(second.Block1.NUM, Is.EqualTo(1));
                Assert.That(second.Block2, Is.Null, "The upload must not fork into a response-body download.");
                Response continued = CreateAck(second, StatusCode.Continue);
                continued.SetBlock1(0, true, 1);
                channel.Emit(Spec.Encode(continued), Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(3));
                Request last = (Request)Spec.Decode(channel.Sent[2]);
                Assert.That(last.Block1.NUM, Is.EqualTo(2));
                Assert.That(last.Block1.M, Is.False);
                Response finished = CreateAck(last, StatusCode.Changed);
                finished.SetBlock1(0, false, 2);
                finished.SetPayload("done", MediaType.TextPlain);
                channel.Emit(Spec.Encode(finished), Peer);

                Assert.That(responses.Count, Is.EqualTo(1));
                Assert.That(responses[0].PayloadString, Is.EqualTo("done"));
                Assert.That(request.IsCancelled, Is.False);
            }
        }

        [TestCase(7, 0, false)]
        [TestCase(0, 1, false)]
        [TestCase(0, 0, true)]
        public void InvalidCurrentBlock1Acknowledgement_NotifiesFailure(Int32 szx, Int32 number, Boolean advanceFirst)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
                List<Response> responses = new List<Response>();
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                client.SendAsync(request, response => responses.Add(response), reason => failures.Add(reason));
                Request current = (Request)Spec.Decode(channel.Sent[0]);
                if (advanceFirst)
                {
                    Response firstAck = CreateAck(current, StatusCode.Continue);
                    firstAck.SetBlock1(0, true, 0);
                    channel.Emit(Spec.Encode(firstAck), Peer);
                    current = (Request)Spec.Decode(channel.Sent[1]);
                }
                Int32 sentBeforeInvalid = channel.Sent.Count;
                Response invalid = CreateAck(current, StatusCode.Continue);
                invalid.SetBlock1(szx, true, number);
                channel.Emit(Spec.Encode(invalid), Peer);

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(request.WaitForResponse(0), Is.Null);
                Assert.That(failures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
                Assert.That(responses, Is.Empty);
                Assert.That(channel.Sent.Count, Is.EqualTo(sentBeforeInvalid));
            }
        }

        [Test]
        public void DuplicateOldAcknowledgement_DoesNotCancelCurrentUpload()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                client.SendAsync(request, fail: reason => failures.Add(reason));
                Request first = (Request)Spec.Decode(channel.Sent[0]);
                Response firstAck = CreateAck(first, StatusCode.Continue);
                firstAck.SetBlock1(0, true, 0);
                channel.Emit(Spec.Encode(firstAck), Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                channel.Emit(Spec.Encode(firstAck), Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(2));
                Assert.That(request.IsCancelled, Is.False);
                Assert.That(failures, Is.Empty);

                Request second = (Request)Spec.Decode(channel.Sent[1]);
                Response secondAck = CreateAck(second, StatusCode.Continue);
                secondAck.SetBlock1(0, true, 1);
                channel.Emit(Spec.Encode(secondAck), Peer);
                Assert.That(channel.Sent.Count, Is.EqualTo(3));
                request.Cancel();
            }
        }

        [Test]
        public void UnsolicitedBlock1ContinueToGet_IsProtocolFailure()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/value"), config) { EndPoint = endpoint };
                Request request = Request.NewGet();
                List<Response> responses = new List<Response>();
                List<CoapClient.FailReason> failures = new List<CoapClient.FailReason>();
                client.SendAsync(request, response => responses.Add(response), reason => failures.Add(reason));
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response invalid = CreateAck(sent, StatusCode.Continue);
                invalid.SetBlock1(0, true, 0);
                channel.Emit(Spec.Encode(invalid), Peer);

                Assert.That(request.IsCancelled, Is.True);
                Assert.That(responses, Is.Empty);
                Assert.That(failures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled }));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ManualBlock1AndEntityTooLargeHint_AreDelivered(Boolean manualBlock1)
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = CreateConfig();
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(new Uri("coap://127.0.0.1:5683/upload"), config) { EndPoint = endpoint };
                Request request = Request.NewPost();
                request.SetPayload(new Byte[manualBlock1 ? 16 : 8], MediaType.ApplicationOctetStream);
                if (manualBlock1)
                    request.SetBlock1(0, true, 0);
                List<Response> responses = new List<Response>();
                client.SendAsync(request, response => responses.Add(response), reason => Assert.Fail(reason.ToString()));
                Request sent = (Request)Spec.Decode(channel.Sent[0]);
                Response reply = CreateAck(sent, manualBlock1 ? StatusCode.Continue : StatusCode.RequestEntityTooLarge);
                reply.SetBlock1(0, manualBlock1, 0);
                channel.Emit(Spec.Encode(reply), Peer);

                Assert.That(request.IsCancelled, Is.False);
                Assert.That(responses.Count, Is.EqualTo(1));
                Assert.That(responses[0].StatusCode, Is.EqualTo(reply.StatusCode));
                Assert.That(channel.Sent.Count, Is.EqualTo(1));
            }
        }

        private static Response CreateAck(Request request, StatusCode code)
        {
            Response response = Response.CreateResponse(request, code);
            response.Type = MessageType.ACK;
            response.ID = request.ID;
            return response;
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
