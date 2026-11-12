/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 * 
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;
using Microsoft.Extensions.Logging;
using CoAP.Net;

namespace CoAP.Stack
{
    public class BlockwiseLayer : AbstractLayer, IDisposable
    {
        static readonly ILogger Log = CoapLogging.CreateLogger(typeof(BlockwiseLayer));
        private static readonly Object TerminatingKey = new Object();

        private Int32 _maxMessageSize;
        private Int32 _defaultBlockSize;
        private Int32 _blockTimeout;
        private readonly ICoapConfig _config;
        private readonly ConcurrentDictionary<Exchange, CleanupRegistration> _activeExchanges = new ConcurrentDictionary<Exchange, CleanupRegistration>();
        private Int32 _disposed;

        /// <summary>
        /// Constructs a new blockwise layer.
        /// </summary>
        public BlockwiseLayer(ICoapConfig config)
        {
            _config = config;
            _maxMessageSize = config.MaxMessageSize;
            _defaultBlockSize = config.DefaultBlockSize;
            _blockTimeout = config.BlockwiseStatusLifetime;
            if (Log.IsEnabled(LogLevel.Debug))
                Log.LogDebug("BlockwiseLayer uses MaxMessageSize: " + _maxMessageSize + " and DefaultBlockSize:" + _defaultBlockSize);

            config.PropertyChanged += ConfigChanged;
        }

        void ConfigChanged(Object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ICoapConfig config = (ICoapConfig)sender;
            if (String.Equals(e.PropertyName, "MaxMessageSize"))
                _maxMessageSize = config.MaxMessageSize;
            else if (String.Equals(e.PropertyName, "DefaultBlockSize"))
                _defaultBlockSize = config.DefaultBlockSize;
            else if (String.Equals(e.PropertyName, "BlockwiseStatusLifetime"))
                _blockTimeout = config.BlockwiseStatusLifetime;
        }

        /// <inheritdoc/>
        public override void SendRequest(INextLayer nextLayer, Exchange exchange, Request request)
        {
            if (IsTerminated(exchange))
                return;
            if (request.HasOption(OptionType.Block2) && request.Block2.NUM > 0)
            {
                // This is the case if the user has explicitly added a block option
                // for random access.
                // Note: We do not regard it as random access when the block num is
                // 0. This is because the user might just want to do early block
                // size negotiation but actually wants to receive all blocks.
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Request carries explicit defined block2 option: create random access blockwise status");
                lock (exchange)
                {
                    if (IsTerminated(exchange))
                        return;
                    BlockwiseStatus status = new BlockwiseStatus(request.ContentFormat);
                    BlockOption block2 = request.Block2;
                    status.CurrentSZX = block2.SZX;
                    status.CurrentNUM = block2.NUM;
                    status.IsRandomAccess = true;
                    exchange.ResponseBlockStatus = status;
                    PrepareBlockCleanup(exchange);
                    if (IsTerminated(exchange))
                    {
                        ReleaseBlockwiseState(exchange, false);
                        return;
                    }
                }
                if (!IsTerminated(exchange))
                    base.SendRequest(nextLayer, exchange, request);
            }
            else if (RequiresBlockwise(request))
            {
                // This must be a large POST or PUT request
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Request payload " + request.PayloadSize + "/" + _maxMessageSize + " requires Blockwise.");
                Request block;
                lock (exchange)
                {
                    BlockwiseStatus status = FindRequestBlockStatus(exchange, request);
                    if (status == null)
                        return;
                    block = GetNextRequestBlock(request, status);
                    exchange.CurrentRequest = block;
                }
                if (!IsTerminated(exchange))
                    base.SendRequest(nextLayer, exchange, block);
            }
            else
            {
                lock (exchange)
                {
                    if (IsTerminated(exchange))
                        return;
                    exchange.CurrentRequest = request;
                }
                if (!IsTerminated(exchange))
                    base.SendRequest(nextLayer, exchange, request);
            }
        }

        /// <inheritdoc/>
        public override void ReceiveRequest(INextLayer nextLayer, Exchange exchange, Request request)
        {
            if (IsTerminated(exchange))
                return;
            if (request.HasOption(OptionType.Block1))
            {
                // This must be a large POST or PUT request
                BlockOption block1 = request.Block1;
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Request contains block1 option " + block1);

                BlockwiseStatus status = FindRequestBlockStatus(exchange, request);
                if (status == null)
                    return;
                lock (exchange)
                {
                    if (IsTerminated(exchange) || !Object.ReferenceEquals(status, exchange.RequestBlockStatus))
                        return;
                    if (block1.NUM == 0 && status.CurrentNUM > 0)
                    {
                        // reset the blockwise transfer
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Block1 num is 0, the client has restarted the blockwise transfer. Reset status.");
                        status = new BlockwiseStatus(request.ContentType);
                        exchange.RequestBlockStatus = status;
                    }
                }

                if (block1.NUM == status.CurrentNUM)
                {
                    if (request.ContentType == status.ContentFormat)
                    {
                        lock (exchange)
                        {
                            if (IsTerminated(exchange) || !Object.ReferenceEquals(status, exchange.RequestBlockStatus))
                                return;
                            status.AddBlock(request.Payload);
                        }
                    }
                    else
                    {
                        Response error = Response.CreateResponse(request, StatusCode.RequestEntityIncomplete);
                        error.AddOption(new BlockOption(OptionType.Block1, block1.NUM, block1.SZX, block1.M));
                        error.SetPayload("Changed Content-Format");

                        exchange.CurrentResponse = error;
                        base.SendResponse(nextLayer, exchange, error);
                        return;
                    }

                    status.CurrentNUM = status.CurrentNUM + 1;
                    if (block1.M)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("There are more blocks to come. Acknowledge this block.");

                        Response piggybacked = Response.CreateResponse(request, StatusCode.Continue);
                        piggybacked.AddOption(new BlockOption(OptionType.Block1, block1.NUM, block1.SZX, true));
                        piggybacked.Last = false;

                        exchange.CurrentResponse = piggybacked;
                        base.SendResponse(nextLayer, exchange, piggybacked);

                        // do not assemble and deliver the request yet
                    }
                    else
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("This was the last block. Deliver request");

                        // Remember block to acknowledge. TODO: We might make this a boolean flag in status.
                        exchange.Block1ToAck = block1;

                        // Block2 early negotiation
                        EarlyBlock2Negotiation(exchange, request);

                        // Assemble and deliver
                        Request assembled = new Request(request.Method);
                        AssembleMessage(status, assembled, request);

                        lock (exchange)
                        {
                            if (IsTerminated(exchange))
                                return;
                            exchange.Request = assembled;
                        }
                        if (!IsTerminated(exchange))
                            base.ReceiveRequest(nextLayer, exchange, assembled);
                    }
                }
                else
                {
                    // ERROR, wrong number, Incomplete
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning("Wrong block number. Expected " + status.CurrentNUM + " but received " + block1.NUM + ". Respond with 4.08 (Request Entity Incomplete).");
                    Response error = Response.CreateResponse(request, StatusCode.RequestEntityIncomplete);
                    error.AddOption(new BlockOption(OptionType.Block1, block1.NUM, block1.SZX, block1.M));
                    error.SetPayload("Wrong block number");
                    exchange.CurrentResponse = error;
                    base.SendResponse(nextLayer, exchange, error);
                }
            }
            else if (exchange.Response != null && request.HasOption(OptionType.Block2))
            {
                // The response has already been generated and the client just wants
                // the next block of it
                BlockOption block2 = request.Block2;
                Response response = exchange.Response;
                BlockwiseStatus status = FindResponseBlockStatus(exchange, response);
                if (status == null)
                    return;
                status.CurrentNUM = block2.NUM;
                status.CurrentSZX = block2.SZX;

                Response block = GetNextResponseBlock(response, status);
                block.Token = request.Token;
                block.RemoveOptions(OptionType.Observe);

                if (status.Complete)
                {
                    // clean up blockwise status
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Ongoing is complete " + status);
                    ReleaseBlockwiseState(exchange, response.HasOption(OptionType.Observe) && Code.IsSuccess(response.Code));
                }
                else
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Ongoing is continuing " + status);
                }

                exchange.CurrentResponse = block;
                base.SendResponse(nextLayer, exchange, block);

            }
            else
            {
                EarlyBlock2Negotiation(exchange, request);

                exchange.Request = request;
                base.ReceiveRequest(nextLayer, exchange, request);
            }
        }

        /// <inheritdoc/>
        public override void SendResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            if (IsTerminated(exchange))
                return;
            BlockOption block1 = exchange.Block1ToAck;
            if (block1 != null)
                exchange.Block1ToAck = null;

            if (RequiresBlockwise(exchange, response))
            {
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Response payload " + response.PayloadSize + "/" + _maxMessageSize + " requires Blockwise");

                BlockwiseStatus status = FindResponseBlockStatus(exchange, response);
                if (status == null)
                    return;

                Response block = GetNextResponseBlock(response, status);

                if (block1 != null) // in case we still have to ack the last block1
                    block.SetOption(block1);
                if (block.Token == null)
                    block.Token = exchange.Request.Token;

                if (status.Complete)
                {
                    // clean up blockwise status
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Ongoing finished on first block " + status);
                    ReleaseBlockwiseState(exchange, response.HasOption(OptionType.Observe) && Code.IsSuccess(response.Code));
                }
                else
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Ongoing started " + status);
                }

                exchange.CurrentResponse = block;
                base.SendResponse(nextLayer, exchange, block);
            }
            else
            {
                if (block1 != null)
                    response.SetOption(block1);
                exchange.CurrentResponse = response;
                // Block1 transfer completed
                ReleaseBlockwiseState(exchange, response.HasOption(OptionType.Observe) && Code.IsSuccess(response.Code));
                base.SendResponse(nextLayer, exchange, response);
            }
        }

        /// <inheritdoc/>
        public override void ReceiveResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            // The matcher/reliability layers normally consume duplicates before
            // this layer. Never let an explicitly marked duplicate end an upload.
            if (response.Duplicate)
                return;

            // do not continue fetching blocks if canceled
            if (exchange.Request.IsCancelled)
            {
                // reject (in particular for Block+Observe)
                if (response.Type != MessageType.ACK)
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Rejecting blockwise transfer for canceled Exchange");
                    EmptyMessage rst = EmptyMessage.NewRST(response);
                    SendEmptyMessage(nextLayer, exchange, rst);
                    // Matcher sets exchange as complete when RST is sent
                }
                return;
            }

            if (exchange.Complete || exchange.Request.IsTimedOut || exchange.Request.IsRejected)
                return;

            if (!response.HasOption(OptionType.Block1) && !response.HasOption(OptionType.Block2))
            {
                // There is no block1 or block2 option, therefore it is a normal response
                DeliverResponse(nextLayer, exchange, response);
                return;
            }

            BlockOption block1 = response.Block1;
            if (block1 != null)
            {
                if (Log.IsEnabled(LogLevel.Debug))
                    Log.LogDebug("Response acknowledges block " + block1);

                BlockwiseStatus status = exchange.RequestBlockStatus;
                if (status != null && block1.SZX == 7)
                {
                    // Reliability has already acknowledged this current response;
                    // abandoning it without a terminal signal would strand upload.
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning("Cancelling upload after Block1 response with reserved SZX 7.");
                    CancelBlockwiseUpload(exchange);
                    return;
                }

                // Old message IDs and duplicates are filtered below this layer.
                // A response matched to the outstanding message but acknowledging
                // a different block is a protocol error, not a pending response.
                if (status != null && Code.IsSuccess(response.Code)
                    && block1.NUM != status.CurrentNUM)
                {
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning("Cancelling upload after Block1 response for block " + block1.NUM
                            + "; expected " + status.CurrentNUM + ".");
                    CancelBlockwiseUpload(exchange);
                    return;
                }

                // A normal request cannot complete with an unsolicited non-final
                // Block1 success. Explicit Block1 requests are managed by callers;
                // error responses such as 4.13 with a size hint remain deliverable.
                if (status == null && !exchange.Request.HasOption(OptionType.Block1)
                    && Code.IsSuccess(response.Code)
                    && (response.StatusCode == StatusCode.Continue || block1.M))
                {
                    CancelBlockwiseUpload(exchange);
                    return;
                }

                // A collecting server must not include Block2, and its
                // non-final successes must be Continue. Conversely, Continue
                // cannot be the final outcome after the last request block.
                // Never report any of these protocol errors as upload success.
                if (block1.M && response.HasOption(OptionType.Block2) ||
                    status != null && Code.IsSuccess(response.Code) &&
                    (block1.M && response.StatusCode != StatusCode.Continue ||
                        status.Complete && response.StatusCode == StatusCode.Continue))
                {
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning("Cancelling upload after an invalid Block1 response: "
                            + response.CodeString + " " + block1);
                    CancelBlockwiseUpload(exchange);
                    return;
                }

                // RFC 7959 sections 2.3 and 2.5: atomic uploads use 2.31
                // Continue; a server acting on individual blocks may instead
                // return another success code with its Block1 M bit cleared.
                if (status == null || status.Complete || !Code.IsSuccess(response.Code))
                {
                    exchange.RequestBlockStatus = null;
                    ClearBlockCleanup(exchange);
                    if (!response.HasOption(OptionType.Block2))
                    {
                        DeliverResponse(nextLayer, exchange, response);
                        return;
                    }
                    // A final response can combine a Block1 acknowledgement
                    // with a Block2 response body. Continue reassembly below.
                }
                else
                {
                    // Send next block
                    Int32 currentSize = 1 << (4 + status.CurrentSZX);
                    Int32 nextSZX = Math.Min(status.CurrentSZX, block1.SZX);
                    Int32 nextSize = 1 << (4 + nextSZX);
                    Int32 nextNum = (status.CurrentNUM + 1) * currentSize / nextSize;
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Send next block num = " + nextNum);
                    status.CurrentNUM = nextNum;
                    status.CurrentSZX = nextSZX;
                    Request nextBlock = GetNextRequestBlock(exchange.Request, status);
                    if (nextBlock.Token == null)
                        nextBlock.Token = response.Token; // reuse same token
                    lock (exchange)
                    {
                        if (IsTerminated(exchange))
                            return;
                        exchange.CurrentRequest = nextBlock;
                    }
                    if (!IsTerminated(exchange))
                        base.SendRequest(nextLayer, exchange, nextBlock);
                    // The high-level operation returns the final upload result.
                    // An optional intermediate per-block response body must not
                    // start Block2 retrieval alongside the next Block1 request.
                    return;
                }
            }

            BlockOption block2 = response.Block2;
            if (block2 != null)
            {
                BlockwiseStatus status = FindResponseBlockStatus(exchange, response);
                if (status == null)
                    return;

                if (block2.NUM == status.CurrentNUM)
                {
                    // We got the block we expected :-)
                    lock (exchange)
                    {
                        if (IsTerminated(exchange) || !Object.ReferenceEquals(status, exchange.ResponseBlockStatus))
                            return;
                        status.AddBlock(response.Payload);
                        Int32? obs = response.Observe;
                        if (obs.HasValue)
                            status.Observe = obs.Value;
                    }

                    // notify blocking progress
                    exchange.Request.FireResponding(response);
                    if (IsTerminated(exchange))
                        return;

                    if (status.IsRandomAccess)
                    {
                        // The client has requested this specifc block and we deliver it
                        DeliverResponse(nextLayer, exchange, response);
                    }
                    else if (block2.M)
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Request the next response block");

                        Request request = exchange.Request;
                        Int32 num = block2.NUM + 1;
                        Int32 szx = block2.SZX;
                        Boolean m = false;

                        Request block = new Request(request.Method);
                        // NON could make sense over SMS or similar transports
                        block.Type = request.Type;
                        block.Destination = request.Destination;
                        block.SetOptions(request.GetOptions());
                        block.SetOption(new BlockOption(OptionType.Block2, num, szx, m));
                        // we use the same token to ease traceability (GET without Observe no longer cancels relations)
                        block.Token = response.Token;
                        // make sure not to use Observe for block retrieval
                        block.RemoveOptions(OptionType.Observe);

                        status.CurrentNUM = num;

                        lock (exchange)
                        {
                            if (IsTerminated(exchange) || !Object.ReferenceEquals(status, exchange.ResponseBlockStatus))
                                return;
                            exchange.CurrentRequest = block;
                        }
                        if (!IsTerminated(exchange))
                            base.SendRequest(nextLayer, exchange, block);
                    }
                    else
                    {
                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("We have received all " + status.BlockCount + " blocks of the response. Assemble and deliver.");
                        Response assembled = new Response(response.StatusCode);
                        lock (exchange)
                        {
                            if (IsTerminated(exchange) || !Object.ReferenceEquals(status, exchange.ResponseBlockStatus))
                                return;
                            AssembleMessage(status, assembled, response);
                        }
                        assembled.Type = response.Type;

                        // set overall transfer RTT
                        assembled.RTT = (DateTime.Now - exchange.Timestamp).TotalMilliseconds;

                        // Check if this response is a notification
                        Int32 observe = status.Observe;
                        if (observe != BlockwiseStatus.NoObserve)
                        {
                            assembled.AddOption(Option.Create(OptionType.Observe, observe));
                            // This is necessary for notifications that are sent blockwise:
                            // Reset block number AND container with all blocks
                            exchange.ResponseBlockStatus = null;
                        }

                        if (Log.IsEnabled(LogLevel.Debug))
                            Log.LogDebug("Assembled response: " + assembled);
                        DeliverResponse(nextLayer, exchange, assembled);
                    }

                }
                else
                {
                    // ERROR, wrong block number (server error)
                    // TODO: This scenario is not specified in the draft.
                    // Currently, we reject it and cancel the request.
                    if (Log.IsEnabled(LogLevel.Warning))
                        Log.LogWarning("Wrong block number. Expected " + status.CurrentNUM + " but received " + block2.NUM + ". Reject response; exchange has failed.");
                    if (response.Type == MessageType.CON)
                    {
                        EmptyMessage rst = EmptyMessage.NewRST(response);
                        base.SendEmptyMessage(nextLayer, exchange, rst);
                    }
                    CancelBlockwiseUpload(exchange);
                }
            }
        }

        private void CancelBlockwiseUpload(Exchange exchange)
        {
            Request original;
            Request current;
            lock (exchange)
            {
                if (!TryMarkTerminating(exchange))
                    return;
                original = exchange.Request ?? exchange.CurrentRequest;
                current = exchange.CurrentRequest;
                ReleaseBlockwiseState(exchange, false);
            }
            PublishTermination(exchange, original, current, false, true);
        }

        private void EarlyBlock2Negotiation(Exchange exchange, Request request)
        {
            // Call this method when a request has completely arrived (might have
            // been sent in one piece without blockwise).
            if (request.HasOption(OptionType.Block2))
            {
                lock (exchange)
                {
                    if (IsTerminated(exchange))
                        return;
                    BlockOption block2 = request.Block2;
                    BlockwiseStatus status2 = new BlockwiseStatus(request.ContentType, block2.NUM, block2.SZX);
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Request with early block negotiation " + block2 + ". Create and set new Block2 status: " + status2);
                    exchange.ResponseBlockStatus = status2;
                    PrepareBlockCleanup(exchange);
                    if (IsTerminated(exchange))
                        ReleaseBlockwiseState(exchange, false);
                }
            }
        }

        /// <summary>
        /// Notice:
        /// This method is used by SendRequest and ReceiveRequest.
        /// Be careful, making changes to the status in here.
        /// </summary>
        private BlockwiseStatus FindRequestBlockStatus(Exchange exchange, Request request)
        {
            lock (exchange)
            {
                if (IsTerminated(exchange))
                    return null;
                BlockwiseStatus status = exchange.RequestBlockStatus;
                if (status == null)
                {
                    status = new BlockwiseStatus(request.ContentType);
                    status.CurrentSZX = BlockOption.EncodeSZX(_defaultBlockSize);
                    exchange.RequestBlockStatus = status;
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("There is no assembler status yet. Create and set new Block1 status: " + status);
                }
                else
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Current Block1 status: " + status);
                }
                // sets a timeout to complete exchange
                PrepareBlockCleanup(exchange);
                if (IsTerminated(exchange))
                {
                    ReleaseBlockwiseState(exchange, false);
                    return null;
                }
                return status;
            }
        }

        /// <summary>
        /// Notice:
        /// This method is used by SendResponse and ReceiveResponse.
        /// Be careful, making changes to the status in here.
        /// </summary>
        private BlockwiseStatus FindResponseBlockStatus(Exchange exchange, Response response)
        {
            lock (exchange)
            {
                if (IsTerminated(exchange))
                    return null;
                BlockwiseStatus status = exchange.ResponseBlockStatus;
                if (status == null)
                {
                    status = new BlockwiseStatus(response.ContentType);
                    status.CurrentSZX = BlockOption.EncodeSZX(_defaultBlockSize);
                    exchange.ResponseBlockStatus = status;
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("There is no blockwise status yet. Create and set new Block2 status: " + status);
                }
                else
                {
                    if (Log.IsEnabled(LogLevel.Debug))
                        Log.LogDebug("Current Block2 status: " + status);
                }
                // sets a timeout to complete exchange
                PrepareBlockCleanup(exchange);
                if (IsTerminated(exchange))
                {
                    ReleaseBlockwiseState(exchange, false);
                    return null;
                }
                return status;
            }
        }

        private Boolean IsTerminated(Exchange exchange)
        {
            Request original = exchange.Request ?? exchange.CurrentRequest;
            return System.Threading.Volatile.Read(ref _disposed) != 0 || IsExchangeTerminating(exchange) || exchange.Complete || exchange.TimedOut
                || original != null && (original.IsCancelled || original.IsTimedOut || original.IsRejected);
        }

        internal static Boolean IsExchangeTerminating(Exchange exchange)
        {
            return exchange != null && exchange.Get(TerminatingKey) != null;
        }

        private static Boolean TryMarkTerminating(Exchange exchange)
        {
            if (IsExchangeTerminating(exchange))
                return false;
            exchange.Set(TerminatingKey, true);
            return true;
        }

        private static void PublishTermination(Exchange exchange, Request original, Request current,
            Boolean timedOut, Boolean cancelOriginal)
        {
            List<Exception> failures = new List<Exception>();
            try
            {
                if (timedOut && !exchange.TimedOut)
                    exchange.TimedOut = true;
                else if (!exchange.Complete)
                    exchange.Complete = true;
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
            try
            {
                if (exchange.Origin == Origin.Local && original != null
                    && !original.IsCancelled && !original.IsRejected && !original.IsTimedOut)
                {
                    if (timedOut)
                        original.IsTimedOut = true;
                    else if (cancelOriginal)
                        original.Cancel();
                }
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
            finally
            {
                try
                {
                    if (current != null && !Object.ReferenceEquals(current, original) && !current.IsCancelled)
                        current.Cancel();
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }
            if (failures.Count > 0)
                throw new AggregateException("Blockwise terminal callback failed.", failures).Flatten();
        }

        private void DeliverResponse(INextLayer nextLayer, Exchange exchange, Response response)
        {
            lock (exchange)
            {
                if (IsTerminated(exchange))
                    return;
                // Retain terminal registration until delivery finishes, so Clear
                // can still cancel an exchange waiting to enter the caller.
                ReleaseBlockwiseState(exchange, true);
                exchange.Response = response;
            }
            try
            {
                if (!IsTerminated(exchange))
                    base.ReceiveResponse(nextLayer, exchange, response);
            }
            finally
            {
                ReleaseBlockwiseState(exchange, !IsTerminated(exchange)
                    && response.HasOption(OptionType.Observe) && Code.IsSuccess(response.Code));
            }
        }

        private Request GetNextRequestBlock(Request request, BlockwiseStatus status)
        {
            Int32 num = status.CurrentNUM;
            Int32 szx = status.CurrentSZX;
            Request block = new Request(request.Method);
            block.SetOptions(request.GetOptions());
            block.Destination = request.Destination;
            block.Token = request.Token;
            block.Type = MessageType.CON;

            Int32 currentSize = 1 << (4 + szx);
            Int32 from = num * currentSize;
            Int32 to = Math.Min((num + 1) * currentSize, request.PayloadSize);
            Int32 length = to - from;
            Byte[] blockPayload = new Byte[length];
            Array.Copy(request.Payload, from, blockPayload, 0, length);
            block.Payload = blockPayload;

            Boolean m = to < request.PayloadSize;
            block.AddOption(new BlockOption(OptionType.Block1, num, szx, m));

            status.Complete = !m;
            return block;
        }

        private Response GetNextResponseBlock(Response response, BlockwiseStatus status)
        {
            Response block;
            Int32 szx = status.CurrentSZX;
            Int32 num = status.CurrentNUM;

            if (response.HasOption(OptionType.Observe))
            {
                // a blockwise notification transmits the first block only
                block = response;
            }
            else
            {
                block = new Response(response.StatusCode);
                block.Destination = response.Destination;
                block.Token = response.Token;
                block.SetOptions(response.GetOptions());
                block.TimedOut += (o, e) => response.IsTimedOut = true;
            }

            Int32 payloadSize = response.PayloadSize;
            Int32 currentSize = 1 << (4 + szx);
            Int32 from = num * currentSize;
            if (payloadSize > 0 && payloadSize > from)
            {
                Int32 to = Math.Min((num + 1) * currentSize, response.PayloadSize);
                Int32 length = to - from;
                Byte[] blockPayload = new Byte[length];
                Boolean m = to < response.PayloadSize;
                block.SetBlock2(szx, m, num);

                // crop payload -- do after calculation of m in case block==response
                Array.Copy(response.Payload, from, blockPayload, 0, length);
                block.Payload = blockPayload;

                // do not complete notifications
                block.Last = !m && !response.HasOption(OptionType.Observe);

                status.Complete = !m;
            }
            else
            {
                block.AddOption(new BlockOption(OptionType.Block2, num, szx, false));
                block.Last = true;
                status.Complete = true;
            }

            return block;
        }

        private void AssembleMessage(BlockwiseStatus status, Message message, Message last)
        {
            // The assembled request will contain the options of the last block
            message.ID = last.ID;
            message.Source = last.Source;
            message.Token = last.Token;
            message.Type = last.Type;
            message.SetOptions(last.GetOptions());

            Int32 length = 0;
            foreach (Byte[] block in status.Blocks)
                length += block.Length;

            Byte[] payload = new Byte[length];
            Int32 offset = 0;
            foreach (Byte[] block in status.Blocks)
            {
                Array.Copy(block, 0, payload, offset, block.Length);
                offset += block.Length;
            }

            message.Payload = payload;
        }

        private Boolean RequiresBlockwise(Request request)
        {
            if (request.Method == Method.PUT || request.Method == Method.POST)
                return request.PayloadSize > _maxMessageSize;
            else
                return false;
        }

        private Boolean RequiresBlockwise(Exchange exchange, Response response)
        {
            return response.PayloadSize > _maxMessageSize
                    || exchange.ResponseBlockStatus != null;
        }

        /// <summary>
        /// Schedules a clean-up task.
        /// Use the <see cref="ICoapConfig.BlockwiseStatusLifetime"/> to set the timeout.
        /// </summary>
        protected void PrepareBlockCleanup(Exchange exchange)
        {
            lock (exchange)
            {
                if (IsTerminated(exchange))
                {
                    if (System.Threading.Volatile.Read(ref _disposed) != 0)
                        QueueDisposedTermination(exchange);
                    return;
                }
                CleanupRegistration registration;
                if (!_activeExchanges.TryGetValue(exchange, out registration))
                {
                    Request original = exchange.Request ?? exchange.CurrentRequest;
                    registration = new CleanupRegistration(original);
                    registration.Terminated = (sender, e) => OnRequestTerminated(exchange, original);
                    registration.Completed = (sender, e) => OnExchangeCompleted(exchange, original);
                    _activeExchanges[exchange] = registration;
                    original.Cancelled += registration.Terminated;
                    original.TimedOut += registration.Terminated;
                    original.Rejected += registration.Terminated;
                    exchange.Completed += registration.Completed;
                }

                if (System.Threading.Volatile.Read(ref _disposed) != 0)
                {
                    QueueDisposedTermination(exchange);
                    return;
                }

                ClearBlockCleanup(exchange);
                System.Timers.Timer timer = new System.Timers.Timer(_blockTimeout) { AutoReset = false };
                timer.Elapsed += (sender, e) => BlockwiseTimeout(exchange, timer);
                exchange.Set("BlockCleanupTimer", timer);
                timer.Start();
            }
        }

        /// <summary>
        /// Clears the clean-up task.
        /// </summary>
        protected void ClearBlockCleanup(Exchange exchange)
        {
            System.Timers.Timer timer = exchange.Remove("BlockCleanupTimer") as System.Timers.Timer;
            if (timer != null)
            {
                try
                {
                    timer.Stop();
                    timer.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }
            }
        }

        private void BlockwiseTimeout(Exchange exchange, System.Timers.Timer timer)
        {
            Request original;
            Request current;
            lock (exchange)
            {
                CleanupRegistration registration;
                if (!_activeExchanges.TryGetValue(exchange, out registration)
                    || !Object.ReferenceEquals(exchange.Get("BlockCleanupTimer"), timer))
                    return;
                original = registration.Request;
                if (exchange.Complete || original.IsCancelled || original.IsRejected || original.IsTimedOut)
                {
                    ReleaseBlockwiseState(exchange, false);
                    return;
                }
                if (!TryMarkTerminating(exchange))
                    return;
                current = exchange.CurrentRequest;
                ReleaseBlockwiseState(exchange, false);
            }

            PublishTermination(exchange, original, current, true, false);
        }

        private void OnRequestTerminated(Exchange exchange, Request original)
        {
            Request current;
            lock (exchange)
            {
                if (!TryMarkTerminating(exchange))
                    return;
                current = exchange.CurrentRequest;
                ReleaseBlockwiseState(exchange, false);
            }
            PublishTermination(exchange, original, current, original.IsTimedOut, false);
        }

        private void OnExchangeCompleted(Exchange exchange, Request original)
        {
            lock (exchange)
            {
                // Matcher may complete a successful server exchange before its
                // final ACK is sent. Only cancellation/timeout uses the tombstone.
                if (exchange.TimedOut && !TryMarkTerminating(exchange))
                    return;
                ReleaseBlockwiseState(exchange, false);
            }
            if (exchange.TimedOut && exchange.Origin == Origin.Local
                && !original.IsTimedOut && !original.IsCancelled && !original.IsRejected)
                original.IsTimedOut = true;
        }

        private void ReleaseBlockwiseState(Exchange exchange, Boolean keepObservation)
        {
            lock (exchange)
            {
                exchange.RequestBlockStatus = null;
                exchange.ResponseBlockStatus = null;
                exchange.Block1ToAck = null;
                ClearBlockCleanup(exchange);
                if (keepObservation)
                    return;
                CleanupRegistration registration;
                if (_activeExchanges.TryRemove(exchange, out registration))
                {
                    registration.Request.Cancelled -= registration.Terminated;
                    registration.Request.TimedOut -= registration.Terminated;
                    registration.Request.Rejected -= registration.Terminated;
                    exchange.Completed -= registration.Completed;
                }
            }
        }

        /// <summary>取消当前分块交换并释放其状态和计时器，允许随后重新开始交换。</summary>
        public void Clear()
        {
            List<(Exchange Exchange, Request Original, Request Current)> notifications =
                new List<(Exchange, Request, Request)>();
            foreach (var entry in _activeExchanges.ToArray())
            {
                Exchange exchange = entry.Key;
                Request original = entry.Value.Request;
                Request current;
                lock (exchange)
                {
                    CleanupRegistration currentRegistration;
                    if (!_activeExchanges.TryGetValue(exchange, out currentRegistration)
                        || !Object.ReferenceEquals(currentRegistration, entry.Value))
                        continue;
                    if (!TryMarkTerminating(exchange))
                        continue;
                    current = exchange.CurrentRequest;
                    ReleaseBlockwiseState(exchange, false);
                }
                notifications.Add((exchange, original, current));
            }

            // Finish releasing the whole snapshot before entering any user
            // callback. One failing callback must not strand later exchanges.
            List<Exception> failures = new List<Exception>();
            foreach (var notification in notifications)
            {
                try
                {
                    PublishTermination(notification.Exchange, notification.Original, notification.Current, false, true);
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }
            if (failures.Count > 0)
                throw new AggregateException("Blockwise cleanup callback failed.", failures).Flatten();
        }

        private void QueueDisposedTermination(Exchange exchange)
        {
            if (!TryMarkTerminating(exchange))
                return;
            Request original = exchange.Request ?? exchange.CurrentRequest;
            Request current = exchange.CurrentRequest;
            ReleaseBlockwiseState(exchange, false);
            // Prepare can be called under a caller's exchange lock. Publish on
            // the pool only for this disposal race, after reserving sole ownership.
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    PublishTermination(exchange, original, current, false, true);
                }
                catch (Exception error)
                {
                    // There is no synchronous caller for this disposal race.
                    // Keep callback failures visible without terminating the pool.
                    Log.LogError(error, "Blockwise disposal callback failed after asynchronous cleanup.");
                }
            });
        }

        /// <summary>取消所有分块交换并解除配置订阅。</summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            try
            {
                Clear();
            }
            finally
            {
                _config.PropertyChanged -= ConfigChanged;
            }
        }

        private sealed class CleanupRegistration
        {
            public CleanupRegistration(Request request) { Request = request; }
            public Request Request { get; }
            public EventHandler Terminated { get; set; }
            public EventHandler Completed { get; set; }
        }
    }
}
