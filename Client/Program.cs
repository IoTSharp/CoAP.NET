using System;

namespace Client
{
    using System.Threading.Tasks;
    using CoAP;

    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new CoapClient(new Uri("coap://127.0.0.1:5683/abc/def/ghi"));
            while (true)
            {
                client.Get();
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    }
}
