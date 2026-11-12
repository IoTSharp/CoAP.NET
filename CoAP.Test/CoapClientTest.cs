using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
#if !NETFX_CORE
using NUnit.Framework;
using TestClass = NUnit.Framework.TestFixtureAttribute;
using TestMethod = NUnit.Framework.TestAttribute;
using TestInitialize = NUnit.Framework.SetUpAttribute;
using TestCleanup = NUnit.Framework.TearDownAttribute;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CoAP.Net;
using CoAP.Server;
using CoAP.Server.Resources;

namespace CoAP
{
    [TestClass]
    public class CoapClientTest
    {
        static readonly String TARGET = "storage";
        static readonly String CONTENT_1 = "one";
        static readonly String CONTENT_2 = "two";
        static readonly String CONTENT_3 = "three";
        static readonly String CONTENT_4 = "four";
        static readonly String QUERY_UPPER_CASE = "uppercase";

        Int32 _serverPort;
        CoapServer _server;
        Resource _resource;
        String _expected;
        Int32 _notifications;
        Boolean _failed;

        [TestInitialize]
        public void SetupServer()
        {
            _notifications = 0;
            _failed = false;
            CreateServer();
        }

        [TestCleanup]
        public void ShutdownServer()
        {
            _server.Dispose();
        }

        [TestMethod]
        public void TestSynchronousCall()
        {
            Uri uri = new Uri("coap://localhost:" + _serverPort + "/" + TARGET);
            CoapClient client = new CoapClient(uri);

            // Check that we get the right content when calling get()
            String resp1 = client.Get().ResponseText;
            Assert.AreEqual(CONTENT_1, resp1);

            String resp2 = client.Get().ResponseText;
            Assert.AreEqual(CONTENT_1, resp2);

            // Change the content to "two" and check
            String resp3 = client.Post(CONTENT_2).ResponseText;
            Assert.AreEqual(CONTENT_1, resp3);

            String resp4 = client.Get().ResponseText;
            Assert.AreEqual(CONTENT_2, resp4);

            // Observe the resource
            _expected = CONTENT_2;
            CoapObserveRelation obs1 = client.Observe(response =>
                {
                    Interlocked.Increment(ref _notifications);
                    String payload = response.ResponseText;
                    Assert.AreEqual(_expected, payload);
                    Assert.IsTrue(response.HasOption(OptionType.Observe));
                }, Fail);
            Assert.IsFalse(obs1.Canceled);

            Thread.Sleep(100);
            _resource.Changed();
            Thread.Sleep(100);
            _resource.Changed();
            Thread.Sleep(100);
            _resource.Changed();

            Thread.Sleep(100);
            _expected = CONTENT_3;
            String resp5 = client.Post(CONTENT_3).ResponseText;
            Assert.AreEqual(CONTENT_2, resp5);

            // Try a put and receive a METHOD_NOT_ALLOWED
            StatusCode code6 = client.Put(CONTENT_4).StatusCode;
            Assert.AreEqual(StatusCode.MethodNotAllowed, code6);

            // Cancel observe relation of obs1 and check that it does no longer receive notifications
            Thread.Sleep(100);
            _expected = null; // The next notification would now cause a failure
            obs1.ReactiveCancel();
            Thread.Sleep(100);
            _resource.Changed();

            // Make another post
            Thread.Sleep(100);
            String resp7 = client.Post(CONTENT_4).ResponseText;
            Assert.AreEqual(CONTENT_3, resp7);

            // Try to use the builder and add a query
            UriBuilder ub = new UriBuilder("coap", "localhost", _serverPort, TARGET);
            ub.Query = QUERY_UPPER_CASE;

            String resp8 = new CoapClient(ub.Uri).Get().ResponseText;
            Assert.AreEqual(CONTENT_4.ToUpper(), resp8);

            // Check that we indeed received 5 notifications
            // 1 from origin GET request, 3 x from changed(), 1 from post()
            Thread.Sleep(100);
            Assert.AreEqual(5, _notifications);
            Assert.IsFalse(_failed);
        }

        [TestMethod]
        public void TestAsynchronousCall()
        {
            Uri uri = new Uri("coap://localhost:" + _serverPort + "/" + TARGET);
            CoapClient client = new CoapClient(uri);
            ConcurrentQueue<CoapClient.FailReason> errors = new ConcurrentQueue<CoapClient.FailReason>();
            EventHandler<CoapClient.ErrorEventArgs> recordError = (sender, args) =>
            {
                errors.Enqueue(args.Reason);
                Volatile.Write(ref _failed, true);
            };
            client.Error += recordError;

            // Each operation advances only after its actual callback response.
            Assert.AreEqual(CONTENT_1, WaitForResponse(done => client.GetAsync(done)).ResponseText);
            Assert.AreEqual(CONTENT_1, WaitForResponse(done => client.GetAsync(done)).ResponseText);
            Assert.AreEqual(CONTENT_1, WaitForResponse(done => client.PostAsync(CONTENT_2, done)).ResponseText);
            Assert.AreEqual(CONTENT_2, WaitForResponse(done => client.GetAsync(done)).ResponseText);

            ConcurrentQueue<Response> notifications = new ConcurrentQueue<Response>();
            Object notificationGate = new Object();
            Boolean acceptingNotifications = true;
            CoapObserveRelation observe = null;
            using (AutoResetEvent notificationArrived = new AutoResetEvent(false))
            {
                try
                {
                    observe = client.ObserveAsync(response =>
                    {
                        lock (notificationGate)
                        {
                            if (!acceptingNotifications)
                                return;
                            notifications.Enqueue(response);
                            Interlocked.Increment(ref _notifications);
                            notificationArrived.Set();
                        }
                    });
                    Assert.IsFalse(observe.Canceled);
                    AssertNotification(notifications, notificationArrived, CONTENT_2);

                    // Registration plus these three changes account for four notifications.
                    for (Int32 change = 0; change < 3; change++)
                    {
                        _resource.Changed();
                        AssertNotification(notifications, notificationArrived, CONTENT_2);
                    }

                    Assert.AreEqual(CONTENT_2,
                        WaitForResponse(done => client.PostAsync(CONTENT_3, done)).ResponseText);
                    AssertNotification(notifications, notificationArrived, CONTENT_3);

                    Assert.AreEqual(StatusCode.MethodNotAllowed,
                        WaitForResponse(done => client.PutAsync(CONTENT_4, done)).StatusCode);

                    observe.ReactiveCancel();
                    _resource.Changed();
                    Assert.AreEqual(CONTENT_3,
                        WaitForResponse(done => client.PostAsync(CONTENT_4, done)).ResponseText);

                    UriBuilder ub = new UriBuilder("coap", "localhost", _serverPort, TARGET);
                    ub.Query = QUERY_UPPER_CASE;
                    CoapClient uppercaseClient = new CoapClient(ub.Uri);
                    uppercaseClient.Error += recordError;
                    try
                    {
                        Assert.AreEqual(CONTENT_4.ToUpper(),
                            WaitForResponse(done => uppercaseClient.GetAsync(done)).ResponseText);
                    }
                    finally
                    {
                        uppercaseClient.Error -= recordError;
                    }

                    // A negative observation window checks cancellation without driving sequencing.
                    Assert.IsFalse(notificationArrived.WaitOne(TimeSpan.FromMilliseconds(200)),
                        "A notification arrived after ReactiveCancel.");
                    Assert.IsTrue(notifications.IsEmpty);
                    Assert.AreEqual(5, Volatile.Read(ref _notifications));
                    Assert.IsTrue(errors.IsEmpty, "An asynchronous client operation reported an error.");
                    Assert.IsFalse(Volatile.Read(ref _failed));
                }
                finally
                {
                    observe?.ReactiveCancel();
                    lock (notificationGate)
                        acceptingNotifications = false;
                    client.Error -= recordError;
                }
            }
        }

        private static Response WaitForResponse(Action<Action<Response>> send)
        {
            Object responseGate = new Object();
            Boolean completed = false;
            Response received = null;
            using (ManualResetEventSlim responseArrived = new ManualResetEventSlim(false))
            {
                try
                {
                    send(response =>
                    {
                        lock (responseGate)
                        {
                            if (completed)
                                return;
                            received = response;
                            responseArrived.Set();
                        }
                    });
                    Assert.IsTrue(responseArrived.Wait(TimeSpan.FromSeconds(5)),
                        "The asynchronous request did not return a response within five seconds.");
                    Assert.IsNotNull(received);
                    return received;
                }
                finally
                {
                    // A late callback after a failed wait must not use the disposed event.
                    lock (responseGate)
                        completed = true;
                }
            }
        }

        private static void AssertNotification(ConcurrentQueue<Response> notifications,
            AutoResetEvent notificationArrived, String expected)
        {
            Assert.IsTrue(notificationArrived.WaitOne(TimeSpan.FromSeconds(5)),
                "The expected Observe notification did not arrive within five seconds.");
            Assert.IsTrue(notifications.TryDequeue(out Response response));
            Assert.AreEqual(expected, response.ResponseText);
            Assert.IsTrue(response.HasOption(OptionType.Observe));
        }

        private void Fail(CoapClient.FailReason reason)
        {
            _failed = true;
            Assert.Fail();
        }

        private void CreateServer()
        {
            CoAPEndPoint endpoint = new CoAPEndPoint(0);
            _resource = new StorageResource(TARGET, CONTENT_1);
            _server = new CoapServer();
            _server.Add(_resource);

            _server.AddEndPoint(endpoint);
            _server.Start();
            _serverPort = ((System.Net.IPEndPoint)endpoint.LocalEndPoint).Port;
        }

        class StorageResource : Resource
        {
            private String _content;

            public StorageResource(String name, String content)
                : base(name)
            {
                _content = content;
                Observable = true;
            }

            protected override void DoGet(CoapExchange exchange)
            {
                IEnumerable<String> queries = exchange.Request.UriQueries;
                String c = _content;
                foreach (String q in queries)
                    if (QUERY_UPPER_CASE.Equals(q))
                        c = _content.ToUpper();

                exchange.Respond(c);
            }

            protected override void DoPost(CoapExchange exchange)
            {
                String old = _content;
                _content = exchange.Request.PayloadString;
                exchange.Respond(StatusCode.Changed, old);
                Changed();
            }
        }
    }
}
