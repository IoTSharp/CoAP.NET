namespace Server
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using WorldDirect.CoAP;
    using WorldDirect.CoAP.V1.Messages;

    public class X
    {
        private readonly IEnumerable<IMessageSerializer> serializers;
        private readonly TransformBlock<Task<UdpReceiveResult>, CoapMessage> block;

        public X(IEnumerable<IMessageSerializer> serializers)
        {
            this.serializers = serializers;
            this.block = new TransformBlock<Task<UdpReceiveResult>, CoapMessage>(this.XAsync);
        }

        public Task EnqueueAsync(Task<UdpReceiveResult> item, CancellationToken ct = default)
        {
            return this.block.SendAsync(item, ct);
        }

        public async Task<CoapMessage> GetMessageAsync(CancellationToken ct = default)
        {
            while (await this.block.OutputAvailableAsync(ct).ConfigureAwait(false))
            {
                var message = await this.block.ReceiveAsync(ct).ConfigureAwait(false);
                return message;
            }

            return default;
        }

        private async Task<CoapMessage> XAsync(Task<UdpReceiveResult> result)
        {
            var x = await result.ConfigureAwait(false);
            var serializer = this.serializers.Single(s => s.CanDeserialize(x));
            return serializer.Deserialize(x.Buffer);
        }
    }
}