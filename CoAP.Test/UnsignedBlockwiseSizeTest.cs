using System;
using CoAP.Net;
using CoAP.Stack;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class UnsignedBlockwiseSizeTest
    {
        [TestCase(0U)]
        [TestCase(2147483647U)]
        [TestCase(2147483648U)]
        [TestCase(UInt32.MaxValue)]
        public void IncomingBlock1PreservesFirstSizeHintWithoutAllocatingIt(uint size)
        {
            Request first = Request.NewPost();
            first.Token = new byte[] { 1 };
            first.SetPayload(new byte[16], MediaType.ApplicationOctetStream);
            first.SetBlock1(0, true, 0);
            first.Size1Unsigned = size;
            Exchange exchange = new Exchange(first, Origin.Remote) { Request = first };
            RecordingLayer next = new RecordingLayer();
            using BlockwiseLayer layer = Layer();
            layer.ReceiveRequest(next, exchange, first);

            Request last = Request.NewPost();
            last.Token = first.Token;
            last.SetPayload(new byte[1], MediaType.ApplicationOctetStream);
            last.SetBlock1(0, false, 1);
            layer.ReceiveRequest(next, exchange, last);

            Assert.That(next.ReceivedRequest.Size1Unsigned, Is.EqualTo(size));
            Assert.That(next.ReceivedRequest.PayloadSize, Is.EqualTo(17));
        }

        [TestCase(0U)]
        [TestCase(2147483647U)]
        [TestCase(2147483648U)]
        [TestCase(UInt32.MaxValue)]
        public void IncomingBlock2PreservesFirstSizeHintWithoutAllocatingIt(uint size)
        {
            Request request = Request.NewGet();
            request.Token = new byte[] { 1 };
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            using BlockwiseLayer layer = Layer();
            layer.SendRequest(next, exchange, request);

            Response first = new Response(StatusCode.Content) { Token = request.Token };
            first.SetPayload(new byte[16], MediaType.ApplicationOctetStream);
            first.SetBlock2(0, true, 0);
            first.Size2Unsigned = size;
            layer.ReceiveResponse(next, exchange, first);

            Response last = new Response(StatusCode.Content) { Token = request.Token };
            last.SetPayload(new byte[1], MediaType.ApplicationOctetStream);
            last.SetBlock2(0, false, 1);
            layer.ReceiveResponse(next, exchange, last);

            Assert.That(next.ReceivedResponse.Size2Unsigned, Is.EqualTo(size));
            Assert.That(next.ReceivedResponse.PayloadSize, Is.EqualTo(17));
            Assert.That(exchange.ResponseBlockStatus, Is.Null);
        }

        [Test]
        public void OutgoingBlocksCarryUnsignedHints()
        {
            RecordingLayer next = new RecordingLayer();
            using BlockwiseLayer layer = Layer();
            Request request = Request.NewPost();
            request.SetPayload(new byte[32], MediaType.ApplicationOctetStream);
            request.Size1Unsigned = UInt32.MaxValue;
            Exchange upload = new Exchange(request, Origin.Local) { Request = request };
            layer.SendRequest(next, upload, request);
            Assert.That(next.SentRequest.Size1Unsigned, Is.EqualTo(UInt32.MaxValue));

            Request get = Request.NewGet();
            Exchange download = new Exchange(get, Origin.Remote) { Request = get };
            Response response = new Response(StatusCode.Content);
            response.SetPayload(new byte[32], MediaType.ApplicationOctetStream);
            response.Size2Unsigned = UInt32.MaxValue;
            layer.SendResponse(next, download, response);
            Assert.That(next.SentResponse.Size2Unsigned, Is.EqualTo(UInt32.MaxValue));
        }

        private static BlockwiseLayer Layer() => new BlockwiseLayer(new CoapConfig
        {
            MaxMessageSize = 16,
            DefaultBlockSize = 16,
            BlockwiseStatusLifetime = 10000
        });

        private sealed class RecordingLayer : INextLayer
        {
            public Request ReceivedRequest { get; private set; }
            public Response ReceivedResponse { get; private set; }
            public Request SentRequest { get; private set; }
            public Response SentResponse { get; private set; }
            public void SendRequest(Exchange exchange, Request request) => SentRequest = request;
            public void SendResponse(Exchange exchange, Response response) => SentResponse = response;
            public void SendEmptyMessage(Exchange exchange, EmptyMessage message) { }
            public void ReceiveRequest(Exchange exchange, Request request) => ReceivedRequest = request;
            public void ReceiveResponse(Exchange exchange, Response response) => ReceivedResponse = response;
            public void ReceiveEmptyMessage(Exchange exchange, EmptyMessage message) { }
        }
    }
}
