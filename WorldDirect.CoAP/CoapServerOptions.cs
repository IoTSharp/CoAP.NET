namespace WorldDirect.CoAP
{
    using System.Net;

    public class CoapServerOptions
    {
        public int Port { get; set; } = 5683;

        public IPAddress Address { get; set; } = IPAddress.Loopback;
    }
}