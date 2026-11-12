#if COAPALL || COAP18
using System;
using System.Net;
using System.Reflection;
using CoAP.Channel;
using CoAP.Net;
using CoAP.Threading;
using NUnit.Framework;

namespace CoAP.Protocol
{
    [TestFixture]
    [NonParallelizable]
    public sealed class LegacyEndpointIsolationTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void CachedDraft18AndRfcEndpointsKeepIndependentMutableState(Boolean legacyFirst)
        {
            // Cache prepared, non-running endpoints because the production cache
            // factory starts UDP listeners. This test must never start a channel.
            FieldInfo legacyCache = typeof(EndPointManager).GetField("_draft18", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo modernCache = typeof(EndPointManager).GetField("_rfc7252", BindingFlags.NonPublic | BindingFlags.Static);
            Object savedLegacy = legacyCache.GetValue(null);
            Object savedModern = modernCache == null ? null : modernCache.GetValue(null);
            using (CoAPEndPoint legacy = new CoAPEndPoint(new NoNetworkChannel(), new CoapConfig { Spec = Spec.Draft18 }))
            using (CoAPEndPoint modern = new CoAPEndPoint(new NoNetworkChannel(), new CoapConfig { Spec = Spec.RFC7252 }))
            {
                try
                {
                    legacy.Executor = Executors.NoThreading;
                    modern.Executor = Executors.NoThreading;
                    legacyCache.SetValue(null, modernCache == null && !legacyFirst ? modern : legacy);
                    if (modernCache != null)
                        modernCache.SetValue(null, modern);

                    IEndPoint first = legacyFirst ? EndPointManager.Draft18 : EndPointManager.RFC7252;
                    IEndPoint second = legacyFirst ? EndPointManager.RFC7252 : EndPointManager.Draft18;
                    Assert.That(first, Is.Not.SameAs(second));
                    Assert.That(EndPointManager.Default, Is.SameAs(modern));
                    Assert.That(EndPointManager.Draft18, Is.SameAs(legacy));
                    Assert.That(legacy.Spec, Is.SameAs(Spec.Draft18));
                    Assert.That(modern.Spec, Is.SameAs(Spec.RFC7252));

                    legacy.Executor = new UnusedExecutor();
                    ((CoapConfig)legacy.Config).DefaultPort = 12345;
                    legacy.MessageDeliverer = new UnusedDeliverer();
                    Assert.That(modern.Executor, Is.SameAs(Executors.NoThreading));
                    Assert.That(modern.Config.DefaultPort, Is.EqualTo(5683));
                    Assert.That(modern.MessageDeliverer, Is.Not.SameAs(legacy.MessageDeliverer));
                    Assert.That(legacy.Running, Is.False);
                    Assert.That(modern.Running, Is.False);
                }
                finally
                {
                    legacyCache.SetValue(null, savedLegacy);
                    if (modernCache != null)
                        modernCache.SetValue(null, savedModern);
                }
            }
        }

        private sealed class NoNetworkChannel : IChannel
        {
            public event EventHandler<DataReceivedEventArgs> DataReceived { add { } remove { } }
            public System.Net.EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 0);
            public void Start() { Assert.Fail("Endpoint isolation must not start a listener."); }
            public void Stop() { }
            public void Send(Byte[] data, System.Net.EndPoint endpoint) { Assert.Fail("Endpoint isolation must not send traffic."); }
            public void Dispose() { }
        }

        private sealed class UnusedExecutor : IExecutor
        {
            public void Start(Action task) { Assert.Fail("No tasks are expected."); }
            public void Start(Action<Object> task, Object state) { Assert.Fail("No tasks are expected."); }
        }

        private sealed class UnusedDeliverer : IMessageDeliverer
        {
            public void DeliverRequest(Exchange exchange) { Assert.Fail("No requests are expected."); }
            public void DeliverResponse(Exchange exchange, Response response) { Assert.Fail("No responses are expected."); }
        }
    }
}
#endif
