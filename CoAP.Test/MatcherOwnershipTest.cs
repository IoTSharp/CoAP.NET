using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Observe;
using CoAP.Server.Resources;
using CoAP.Threading;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    public sealed class MatcherOwnershipTest
    {
        private static readonly IPEndPoint Peer = new IPEndPoint(IPAddress.Loopback, 5683);
        private static readonly Uri Target = new Uri("coap://127.0.0.1:5683/upload");

        [Test]
        public void StaleLocalCompletionPreservesReplacementToken()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Exchange old = matcher.SendLocal(NewRequest(101, 1));
                Exchange replacement = matcher.SendLocal(NewRequest(102, 1));

                old.Complete = true;

                Assert.That(matcher.ReceiveResponse(Reply(replacement.CurrentRequest, 201)), Is.SameAs(replacement));
            }
        }

        [Test]
        public void StaleLocalCompletionPreservesReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Exchange old = matcher.SendLocal(NewRequest(101, 1));
                Exchange replacement = matcher.SendLocal(NewRequest(101, 2));

                old.Complete = true;

                Assert.That(matcher.ReceiveEmpty(101), Is.SameAs(replacement));
            }
        }

        [Test]
        public void OldResponseCleanupPreservesReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Exchange old = matcher.SendLocal(NewRequest(101, 1));
                Exchange replacement = matcher.SendLocal(NewRequest(101, 2));

                Assert.That(matcher.ReceiveResponse(Reply(old.CurrentRequest, 201)), Is.SameAs(old));
                Assert.That(matcher.ReceiveEmpty(101), Is.SameAs(replacement));
            }
        }

        [Test]
        public void StaleRemoteCompletionPreservesReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Exchange old = matcher.ReceiveRequest(NewRequest(101, 1));
                old.CurrentResponse = new Response(StatusCode.Content)
                {
                    ID = 201,
                    Type = MessageType.CON,
                    Destination = Peer
                };
                matcher.SendResponse(old, old.CurrentResponse);
                Exchange replacement = matcher.SendLocal(NewRequest(201, 2));

                old.Complete = true;

                Assert.That(matcher.ReceiveEmpty(201), Is.SameAs(replacement));
            }
        }

        [Test]
        public void StaleRemoteCompletionPreservesReplacementUri()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Request request = NewRequest(101, 1);
                request.SetBlock1(0, true, 0);
                Exchange old = matcher.ReceiveRequest(request);
                Exchange replacement = new Exchange(NewRequest(102, 2), Origin.Remote);
                Exchange.KeyUri key = new Exchange.KeyUri(request.URI, Peer);
                matcher.Ongoing[key] = replacement;

                old.Complete = true;

                Assert.That(matcher.Ongoing.TryGetValue(key, out Exchange owner), Is.True);
                Assert.That(owner, Is.SameAs(replacement));
            }
        }

        [Test]
        public void CompletedBlock2SendPreservesReplacementUri()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Request request = NewRequest(101, 1);
                Exchange old = new Exchange(request, Origin.Remote) { Request = request };
                Exchange replacement = new Exchange(NewRequest(102, 2), Origin.Remote);
                Exchange.KeyUri key = new Exchange.KeyUri(request.URI, Peer);
                matcher.Ongoing[key] = replacement;
                Response response = new Response(StatusCode.Content)
                {
                    ID = 201,
                    Type = MessageType.ACK,
                    Destination = Peer,
                    Last = false
                };
                response.SetBlock2(0, false, 0);

                matcher.SendResponse(old, response);

                Assert.That(matcher.Ongoing.TryGetValue(key, out Exchange owner), Is.True);
                Assert.That(owner, Is.SameAs(replacement));
            }
        }

        [Test]
        public void OldBlockContinuationPreservesReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Request request = NewRequest(101, 1);
                request.SetBlock1(0, true, 0);
                Exchange old = matcher.ReceiveRequest(request);
                old.CurrentResponse = new Response(StatusCode.Continue) { ID = 201, Type = MessageType.NON };
                Exchange replacement = matcher.SendLocal(NewRequest(201, 2));
                Request continuation = NewRequest(102, 1);
                continuation.SetBlock1(0, false, 1);

                Assert.That(matcher.ReceiveRequest(continuation), Is.SameAs(old));
                Assert.That(matcher.ReceiveEmpty(201), Is.SameAs(replacement));
            }
        }

        [Test]
        public void NotificationCleanupPreservesReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Request request = NewRequest(101, 1);
                Exchange old = matcher.ReceiveRequest(request);
                ObserveRelation relation = new ObserveRelation(new CoapConfig(),
                    new ObservingEndpoint(Peer), new Resource("upload"), old);
                old.Relation = relation;
                Response notification = new Response(StatusCode.Content)
                {
                    ID = 201,
                    Type = MessageType.NON,
                    Destination = Peer,
                    Last = false
                };
                relation.AddNotification(notification);
                matcher.SendResponse(old, notification);
                Exchange replacement = matcher.SendLocal(NewRequest(201, 2));
                matcher.SendResponse(old, new Response(StatusCode.Content)
                {
                    ID = 202,
                    Type = MessageType.ACK,
                    Destination = Peer,
                    Last = false
                });

                Assert.That(matcher.ReceiveEmpty(201), Is.SameAs(replacement));
            }
        }

        [Test]
        public void OwnedCompletionStillRemovesItsOwnMappings()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                Exchange owned = matcher.SendLocal(NewRequest(101, 1));

                owned.Complete = true;

                Assert.That(matcher.ReceiveEmpty(101), Is.Null);
                Assert.That(matcher.ReceiveResponse(Reply(owned.CurrentRequest, 201)), Is.Null);
            }
        }

        [Test]
        public void EmptyReplyCleanupDoesNotDeleteReentrantReplacementMessageId()
        {
            using (MatcherHarness matcher = new MatcherHarness())
            {
                ComparingEndPoint destination = new ComparingEndPoint("peer");
                Request request = NewRequest(101, 1);
                request.Destination = destination;
                Exchange old = matcher.SendLocal(request);
                Exchange replacement = null;
                destination.OnCompare = () => replacement = matcher.SendLocal(NewRequest(101, 2));

                Assert.That(matcher.ReceiveEmpty(101, new ComparingEndPoint("peer")), Is.SameAs(old));

                Assert.That(replacement, Is.Not.Null);
                Assert.That(matcher.ReceiveEmpty(101), Is.SameAs(replacement));
            }
        }

        [Test]
        public void ClearTwoUploadsPreservesReplacementUsingOtherCancelledToken()
        {
            RecordingChannel channel = new RecordingChannel();
            CoapConfig config = new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16,
                AckTimeout = 10000,
                BlockwiseStatusLifetime = 10000
            };
            using (CoAPEndPoint endpoint = new CoAPEndPoint(channel, config) { Executor = Executors.NoThreading })
            {
                endpoint.Start();
                CoapClient client = new CoapClient(Target, config) { EndPoint = endpoint };
                Request first = Upload(1);
                Request second = Upload(2);
                Request replacement = null;
                Int32 replacementStarted = 0;
                Int32 recovered = 0;
                List<CoapClient.FailReason> oldFailures = new List<CoapClient.FailReason>();
                List<CoapClient.FailReason> replacementFailures = new List<CoapClient.FailReason>();
                Action<Request, CoapClient.FailReason> startReplacement = (other, reason) =>
                {
                    oldFailures.Add(reason);
                    if (Interlocked.CompareExchange(ref replacementStarted, 1, 0) != 0)
                        return;
                    replacement = Request.NewGet();
                    replacement.Token = other.Token.ToArray();
                    client.SendAsync(replacement, response => recovered++, failure => replacementFailures.Add(failure));
                };
                client.SendAsync(first, fail: reason => startReplacement(second, reason));
                client.SendAsync(second, fail: reason => startReplacement(first, reason));

                endpoint.Clear();

                Assert.That(first.IsCancelled && second.IsCancelled, Is.True);
                Assert.That(oldFailures, Is.EqualTo(new[] { CoapClient.FailReason.Cancelled, CoapClient.FailReason.Cancelled }));
                Assert.That(replacement, Is.Not.Null);
                Assert.That(channel.Sent, Has.Count.EqualTo(3));
                Request sent = (Request)Spec.Decode(channel.Sent.Last());
                Response reply = Response.CreateResponse(sent, StatusCode.Content);
                reply.Type = MessageType.ACK;
                reply.ID = sent.ID;
                channel.Emit(Spec.Encode(reply), Peer);

                Assert.That(recovered, Is.EqualTo(1));
                Assert.That(replacementFailures, Is.Empty);
            }
        }

        private static Request Upload(Byte token)
        {
            Request request = Request.NewPost();
            request.Token = new[] { token };
            request.SetPayload(new Byte[48], MediaType.ApplicationOctetStream);
            return request;
        }

        private static Request NewRequest(Int32 id, Byte token)
        {
            return new Request(Method.POST)
            {
                ID = id,
                Type = MessageType.CON,
                Token = new[] { token },
                URI = Target,
                Source = Peer,
                Destination = Peer
            };
        }

        private static Response Reply(Request request, Int32 id)
        {
            return new Response(StatusCode.Content)
            {
                ID = id,
                Type = MessageType.NON,
                Token = request.Token,
                Source = Peer
            };
        }

        private sealed class MatcherHarness : IDisposable
        {
            private readonly Object _matcher = Activator.CreateInstance(
                typeof(CoapClient).Assembly.GetType("CoAP.Net.Matcher", true), new CoapConfig());

            public IDictionary<Exchange.KeyUri, Exchange> Ongoing =>
                (IDictionary<Exchange.KeyUri, Exchange>)_matcher.GetType()
                    .GetField("_ongoingExchanges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_matcher);

            public Exchange SendLocal(Request request)
            {
                Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
                Invoke("SendRequest", exchange, request);
                return exchange;
            }

            public Exchange ReceiveRequest(Request request)
            {
                Exchange exchange = (Exchange)Invoke("ReceiveRequest", request);
                exchange.Request = request;
                return exchange;
            }

            public Exchange ReceiveResponse(Response response) => (Exchange)Invoke("ReceiveResponse", response);
            public Exchange ReceiveEmpty(Int32 id, System.Net.EndPoint source = null) => (Exchange)Invoke("ReceiveEmptyMessage",
                new EmptyMessage(MessageType.ACK) { ID = id, Source = source ?? Peer });
            public void SendResponse(Exchange exchange, Response response) => Invoke("SendResponse", exchange, response);
            public void Dispose() => ((IDisposable)_matcher).Dispose();

            private Object Invoke(String method, params Object[] arguments) => _matcher.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(_matcher, arguments);
        }

        private sealed class ComparingEndPoint : System.Net.EndPoint
        {
            private readonly String _name;
            public Action OnCompare;

            public ComparingEndPoint(String name) { _name = name; }

            public override Boolean Equals(Object other)
            {
                Interlocked.Exchange(ref OnCompare, null)?.Invoke();
                return other is ComparingEndPoint endpoint && _name == endpoint._name;
            }

            public override Int32 GetHashCode() => StringComparer.Ordinal.GetHashCode(_name);
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
