using System;
using NUnit.Framework;
using CoAP.Net;
using CoAP.Stack;

namespace CoAP
{
    [TestFixture]
    public sealed class BlockwiseProtocolRegressionTest
    {
        [Test]
        public void ManuallyManagedBlock1ResponseWithoutRequestStatus_IsForwarded()
        {
            Request request = Request.NewPost();
            request.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            Exchange exchange = new Exchange(request, Origin.Local)
            {
                Request = request
            };
            Response response = Response.CreateResponse(request, StatusCode.Continue);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            RecordingLayer next = new RecordingLayer();

            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig());

            Assert.DoesNotThrow(() => layer.ReceiveResponse(next, exchange, response));
            Assert.That(next.ReceivedResponses, Is.EqualTo(1));
            Assert.That(next.SentRequests, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.False);
        }

        [TestCase(StatusCode.Continue, false)]
        [TestCase(StatusCode.Continue, true)]
        [TestCase(StatusCode.Changed, true)]
        public void UnsolicitedNonFinalBlock1SuccessForOrdinaryGet_CancelsWithoutReportingSuccess(StatusCode code, bool more)
        {
            Request request = Request.NewGet();
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            Response response = Response.CreateResponse(request, code);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, more));
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig());

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(next.SentRequests, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
        }

        [Test]
        public void InitialRequestEntityTooLargeWithBlock1Hint_IsDelivered()
        {
            Request request = Request.NewPut();
            request.SetPayload("payload", MediaType.TextPlain);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            Response response = Response.CreateResponse(request, StatusCode.RequestEntityTooLarge);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, false));
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig());

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.ReceivedResponses, Is.EqualTo(1));
            Assert.That(next.LastResponse, Is.SameAs(response));
            Assert.That(next.SentRequests, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.False);
        }

        [TestCase(StatusCode.BadRequest)]
        [TestCase(StatusCode.RequestEntityIncomplete)]
        [TestCase(StatusCode.RequestEntityTooLarge)]
        [TestCase(StatusCode.InternalServerError)]
        public void Block1ErrorResponse_DoesNotContinueUpload(StatusCode statusCode)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[32], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local)
            {
                Request = request
            };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });

            layer.SendRequest(next, exchange, request);
            Assert.That(next.SentRequests, Is.EqualTo(1));

            Response response = Response.CreateResponse(request, statusCode);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(next.ReceivedResponses, Is.EqualTo(1));
            Assert.That(next.LastResponse, Is.SameAs(response));
            Assert.That(exchange.RequestBlockStatus, Is.Null);
        }

        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(0, 7)]
        public void InvalidBlock1Acknowledgement_CancelsUpload(int number, int szx)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local)
            {
                Request = request
            };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });

            layer.SendRequest(next, exchange, request);
            BlockwiseStatus status = exchange.RequestBlockStatus;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.CurrentNUM, Is.EqualTo(0));

            Response response = Response.CreateResponse(request, StatusCode.Continue);
            response.AddOption(new BlockOption(OptionType.Block1, number, szx, true));
            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(status.CurrentNUM, Is.EqualTo(0));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
            Assert.That(exchange.RequestBlockStatus, Is.Null);
        }

        [Test]
        public void ContinueForCurrentBlock_SendsOnlyNextBlock()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local)
            {
                Request = request
            };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });

            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, StatusCode.Continue);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.LastRequest, Is.Not.Null);
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(1));
            Assert.That(exchange.RequestBlockStatus.CurrentNUM, Is.EqualTo(1));
        }

        [TestCase(StatusCode.Created)]
        [TestCase(StatusCode.Changed)]
        public void PerBlockSuccessWithMoreBitCleared_ContinuesUpload(StatusCode statusCode)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, statusCode);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, false));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(1));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntermediateBlock2Body_DoesNotAdvanceOrCompleteTheUpload(bool more)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, StatusCode.Changed);
            response.SetPayload("0123456789abcdef", MediaType.TextPlain);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, false));
            response.AddOption(new BlockOption(OptionType.Block2, 0, 0, more));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.LastRequest.Block1, Is.Not.Null);
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(1));
            Assert.That(exchange.CurrentRequest, Is.SameAs(next.LastRequest));
            Assert.That(exchange.ResponseBlockStatus, Is.Null);
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.False);
        }

        [TestCase(StatusCode.Created)]
        [TestCase(StatusCode.Changed)]
        public void AtomicSuccessWithoutContinue_CancelsUploadWithoutReportingSuccess(StatusCode statusCode)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, statusCode);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
            Assert.That(request.WaitForResponse(0), Is.Null);
            Assert.That(exchange.RequestBlockStatus, Is.Null);
        }

        [TestCase(StatusCode.Continue)]
        [TestCase(StatusCode.Changed)]
        public void AtomicBlock1ResponseWithBlock2_CancelsBeforeSendingAnotherRequest(StatusCode statusCode)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, statusCode);
            response.SetPayload("0123456789abcdef", MediaType.TextPlain);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            response.AddOption(new BlockOption(OptionType.Block2, 0, 0, true));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
            Assert.That(exchange.RequestBlockStatus, Is.Null);
            Assert.That(exchange.ResponseBlockStatus, Is.Null);
        }

        [Test]
        public void ContinueWithReservedBlockSize_CancelsUpload()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response response = Response.CreateResponse(request, StatusCode.Continue);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 7, true));

            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(exchange.RequestBlockStatus, Is.Null);
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateBlock1Response_DoesNotSkipTheOutstandingBlock()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response first = Response.CreateResponse(request, StatusCode.Continue);
            first.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            layer.ReceiveResponse(next, exchange, first);

            first.Duplicate = true;
            layer.ReceiveResponse(next, exchange, first);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(exchange.RequestBlockStatus.CurrentNUM, Is.EqualTo(1));
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateContinueWhileFinalBlockIsOutstanding_DoesNotFinishUpload()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[32], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response first = Response.CreateResponse(request, StatusCode.Continue);
            first.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            layer.ReceiveResponse(next, exchange, first);
            BlockwiseStatus status = exchange.RequestBlockStatus;
            Assert.That(status.Complete, Is.True);

            first.Duplicate = true;
            layer.ReceiveResponse(next, exchange, first);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(exchange.RequestBlockStatus, Is.SameAs(status));

            Response final = Response.CreateResponse(request, StatusCode.Changed);
            final.AddOption(new BlockOption(OptionType.Block1, 1, 0, false));
            layer.ReceiveResponse(next, exchange, final);

            Assert.That(next.ReceivedResponses, Is.EqualTo(1));
            Assert.That(next.LastResponse, Is.SameAs(final));
            Assert.That(exchange.RequestBlockStatus, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContinueForFinalBlock_CancelsUploadWithoutReportingSuccess(bool more)
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[32], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);
            Response first = Response.CreateResponse(request, StatusCode.Continue);
            first.AddOption(new BlockOption(OptionType.Block1, 0, 0, true));
            layer.ReceiveResponse(next, exchange, first);
            Response invalidFinal = Response.CreateResponse(request, StatusCode.Continue);
            invalidFinal.AddOption(new BlockOption(OptionType.Block1, 1, 0, more));

            layer.ReceiveResponse(next, exchange, invalidFinal);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(request.IsCancelled, Is.True);
            Assert.That(exchange.Complete, Is.True);
            Assert.That(request.WaitForResponse(0), Is.Null);
            Assert.That(exchange.RequestBlockStatus, Is.Null);
        }

        [Test]
        public void ContinueWithSmallerBlockSize_PreservesTheByteOffset()
        {
            Request request = Request.NewPost();
            byte[] payload = new byte[96];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)i;
            request.SetPayload(payload, MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 32,
                DefaultBlockSize = 32
            });
            layer.SendRequest(next, exchange, request);
            Response first = Response.CreateResponse(request, StatusCode.Continue);
            first.AddOption(new BlockOption(OptionType.Block1, 0, 1, true));
            layer.ReceiveResponse(next, exchange, first);

            Response smaller = Response.CreateResponse(request, StatusCode.Continue);
            smaller.AddOption(new BlockOption(OptionType.Block1, 1, 0, true));
            layer.ReceiveResponse(next, exchange, smaller);

            Assert.That(next.SentRequests, Is.EqualTo(3));
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(4));
            Assert.That(next.LastRequest.Payload, Is.EqualTo(new byte[]
            {
                64, 65, 66, 67, 68, 69, 70, 71,
                72, 73, 74, 75, 76, 77, 78, 79
            }));
        }

        [Test]
        public void FinalBlock1ResponseWithBlock2_AssemblesTheResponseBody()
        {
            Request request = Request.NewPost();
            Exchange exchange = new Exchange(request, Origin.Local)
            {
                Request = request,
                RequestBlockStatus = new BlockwiseStatus(MediaType.ApplicationOctetStream, 1, 0)
                {
                    Complete = true
                }
            };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig());
            Response first = Response.CreateResponse(request, StatusCode.Changed);
            first.SetPayload("0123456789abcdef", MediaType.TextPlain);
            first.AddOption(new BlockOption(OptionType.Block1, 1, 0, false));
            first.AddOption(new BlockOption(OptionType.Block2, 0, 0, true));

            layer.ReceiveResponse(next, exchange, first);

            Assert.That(next.ReceivedResponses, Is.EqualTo(0));
            Assert.That(next.SentRequests, Is.EqualTo(1));
            Assert.That(next.LastRequest.Block2.NUM, Is.EqualTo(1));

            Response last = Response.CreateResponse(request, StatusCode.Changed);
            last.SetPayload("ghijkl", MediaType.TextPlain);
            last.AddOption(new BlockOption(OptionType.Block2, 1, 0, false));
            layer.ReceiveResponse(next, exchange, last);

            Assert.That(next.ReceivedResponses, Is.EqualTo(1));
            Assert.That(next.LastResponse.PayloadString, Is.EqualTo("0123456789abcdefghijkl"));
        }

        [Test]
        public void ContinueWithLargerBlockPreference_DoesNotResendPreviousBytes()
        {
            Request request = Request.NewPost();
            request.SetPayload(new byte[48], MediaType.ApplicationOctetStream);
            Exchange exchange = new Exchange(request, Origin.Local) { Request = request };
            RecordingLayer next = new RecordingLayer();
            BlockwiseLayer layer = new BlockwiseLayer(new CoapConfig
            {
                MaxMessageSize = 16,
                DefaultBlockSize = 16
            });
            layer.SendRequest(next, exchange, request);

            Response response = Response.CreateResponse(request, StatusCode.Continue);
            response.AddOption(new BlockOption(OptionType.Block1, 0, 1, true));
            layer.ReceiveResponse(next, exchange, response);

            Assert.That(next.SentRequests, Is.EqualTo(2));
            Assert.That(next.LastRequest.Block1.NUM, Is.EqualTo(1));
            Assert.That(next.LastRequest.Block1.Size, Is.EqualTo(16));
        }

        private sealed class RecordingLayer : INextLayer
        {
            public int SentRequests { get; private set; }
            public int ReceivedResponses { get; private set; }
            public Request LastRequest { get; private set; }
            public Response LastResponse { get; private set; }

            public void SendRequest(Exchange exchange, Request request)
            {
                SentRequests++;
                LastRequest = request;
            }

            public void SendResponse(Exchange exchange, Response response)
            {
            }

            public void SendEmptyMessage(Exchange exchange, EmptyMessage message)
            {
            }

            public void ReceiveRequest(Exchange exchange, Request request)
            {
            }

            public void ReceiveResponse(Exchange exchange, Response response)
            {
                ReceivedResponses++;
                LastResponse = response;
            }

            public void ReceiveEmptyMessage(Exchange exchange, EmptyMessage message)
            {
            }
        }
    }
}
