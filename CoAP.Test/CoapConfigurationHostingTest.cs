using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoAP.Net;
using CoAP.Server;
using CoAP.Server.Hosting;
using CoAP.Server.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace CoAP
{
    /// <summary>
    /// 验证配置绑定、宿主监听、日志和 URI 查询的公开集成合同。
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public sealed class CoapConfigurationHostingTest
    {
        private ILoggerFactory _previousLoggerFactory;

        /// <summary>
        /// 保存共享日志入口，避免本测试的宿主影响其他协议测试。
        /// </summary>
        [SetUp]
        public void CaptureLoggerFactory()
        {
            _previousLoggerFactory = CoapLogging.LoggerFactory;
        }

        /// <summary>
        /// 每个用例结束后恢复原日志工厂。
        /// </summary>
        [TearDown]
        public void RestoreLoggerFactory()
        {
            CoapLogging.LoggerFactory = _previousLoggerFactory;
        }

        /// <summary>
        /// 配置入口同时绑定继承的协议设置和 DTLS 设置，未显式指定端口时使用配置默认端口。
        /// </summary>
        [Test]
        public void ConfigurationSectionBindsProtocolAndDtlsSettings()
        {
            var services = new ServiceCollection();
            services.AddCoapServer(Configuration(
                ("Enabled", "false"),
                ("DefaultPort", "15683"),
                ("DefaultSecurePort", "15684"),
                ("DefaultBlockSize", "512"),
                ("MaxMessageSize", "1024"),
                ("AckRandomFactor", "1.25"),
                ("MaxRetransmit", "7"),
                ("ChannelReceivePacketSize", "4096"),
                ("BindAddress", "127.0.0.1"),
                ("Dtls:Enabled", "true"),
                ("Dtls:SessionIdleSeconds", "45"),
                ("Dtls:PskKeys:probe", "test-key")));

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<CoapServerListenOptions>();
            Assert.IsFalse(options.Enabled);
            Assert.AreEqual(15683, options.GetCoapPort());
            Assert.AreEqual(15684, options.GetCoapsPort());
            Assert.AreEqual(512, options.DefaultBlockSize);
            Assert.AreEqual(1024, options.MaxMessageSize);
            Assert.AreEqual(1.25, options.AckRandomFactor);
            Assert.AreEqual(7, options.MaxRetransmit);
            Assert.AreEqual(4096, options.ChannelReceivePacketSize);
            Assert.IsTrue(options.Dtls.Enabled);
            Assert.AreEqual(IPAddress.Loopback, options.GetCoapsBindAddress());
            Assert.AreEqual(TimeSpan.FromSeconds(45), options.Dtls.GetSessionIdleTimeout());
            Assert.AreEqual("test-key", options.Dtls.PskKeys["probe"]);
        }

        /// <summary>
        /// 停用全部传输时不注册服务或打开端口，ASP.NET Core 的兼容入口也保持可调用。
        /// </summary>
        [Test]
        public void DisabledTransportDoesNotRegisterServerAndUseCoapServerIsNoOp()
        {
            var services = new ServiceCollection();
            services.AddCoapServer(Configuration(("Enabled", "false"), ("Dtls:Enabled", "false")));
            services.AddCoapResources();
            using var provider = services.BuildServiceProvider();

            Assert.IsNull(provider.GetService<IServer>());
            Assert.IsNull(provider.GetService<CoapServer>());
            CollectionAssert.IsEmpty(provider.GetServices<IHostedService>());
            Assert.IsFalse(provider.GetRequiredService<CoapServerListenOptions>().HasEnabledTransport);
            var application = new ApplicationBuilder(provider);
            Assert.AreSame(application, application.UseCoapServer());
        }

        /// <summary>
        /// UDP、DTLS 或组合监听只启动配置的 loopback 端点，零端口由操作系统分配。
        /// </summary>
        /// <param name="udp">是否启用 UDP。</param>
        /// <param name="dtls">是否启用 DTLS。</param>
        /// <param name="expectedCount">预期端点数量。</param>
        [TestCase(true, false, 1)]
        [TestCase(false, true, 1)]
        [TestCase(true, true, 2)]
        [Timeout(15000)]
        public async Task ConfiguredTransportsStartOnlyRequestedLoopbackEndpoints(bool udp, bool dtls, int expectedCount)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCoapServer(Configuration(
                ("Enabled", udp.ToString()),
                ("BindAddress", "127.0.0.1"),
                ("Port", "0"),
                ("DefaultBlockSize", "512"),
                ("Dtls:Enabled", dtls.ToString()),
                ("Dtls:Port", "0"),
                ("Dtls:PskKeys:probe", "test-key")));
            using var provider = services.BuildServiceProvider();
            var hosted = provider.GetServices<IHostedService>().Single();
            var options = provider.GetRequiredService<CoapServerListenOptions>();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await hosted.StartAsync(cancellation.Token);
                var endpoints = provider.GetRequiredService<IServer>().EndPoints.Cast<CoAPEndPoint>().ToArray();
                Assert.AreEqual(expectedCount, endpoints.Length);
                foreach (var endpoint in endpoints)
                {
                    var local = (IPEndPoint)endpoint.LocalEndPoint;
                    Assert.AreEqual(IPAddress.Loopback, local.Address);
                    Assert.Greater(local.Port, 0);
                    Assert.AreSame(options, endpoint.Config);
                    Assert.AreEqual(512, endpoint.Config.DefaultBlockSize);
                }
            }
            finally
            {
                await hosted.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// 配置错误必须在注册监听服务之前失败，不能降级到默认地址或端口。
        /// </summary>
        /// <param name="key">无效配置项。</param>
        /// <param name="value">无效配置值。</param>
        [TestCase("BindAddress", "not-an-ip")]
        [TestCase("Port", "-1")]
        [TestCase("Port", "65536")]
        [TestCase("Dtls:BindAddress", "not-an-ip")]
        [TestCase("Dtls:Port", "-1")]
        [TestCase("Dtls:Port", "65536")]
        public void InvalidListenerConfigurationFailsBeforeServerRegistration(string key, string value)
        {
            var configuration = Configuration(
                ("Enabled", "true"),
                ("BindAddress", "127.0.0.1"),
                ("Port", "0"),
                ("Dtls:Enabled", "true"),
                ("Dtls:Port", "0"),
                ("Dtls:PskKeys:probe", "test-key"));
            configuration[key] = value;
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() => services.AddCoapServer(configuration));
            Assert.IsFalse(services.Any(item => item.ServiceType == typeof(IServer) || item.ServiceType == typeof(IHostedService)));
        }

        /// <summary>
        /// DTLS 启用后必须存在有效身份和密钥，空值不能注册可启动的 server。
        /// </summary>
        /// <param name="identity">测试身份；null 表示未提供任何映射。</param>
        /// <param name="key">测试密钥。</param>
        [TestCase(null, null)]
        [TestCase("", "test-key")]
        [TestCase("probe", "")]
        [TestCase("probe", " ")]
        public void InvalidPskConfigurationFailsBeforeServerRegistration(string identity, string key)
        {
            var options = new CoapServerListenOptions
            {
                Enabled = false,
                Dtls = new CoapServerDtlsPskOptions { Enabled = true, BindAddress = "127.0.0.1", Port = 0 }
            };
            if (identity != null)
            {
                options.Dtls.PskKeys[identity] = key;
            }
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() => services.AddCoapServer(options));
            Assert.IsFalse(services.Any(item => item.ServiceType == typeof(IServer) || item.ServiceType == typeof(IHostedService)));
        }

        /// <summary>
        /// 公开超时设置保留默认值，并将过小的值夹紧到 30 秒。
        /// </summary>
        [Test]
        public void DtlsIdleTimeoutPreservesDefaultAndMinimum()
        {
            var options = new CoapServerDtlsPskOptions();
            Assert.AreEqual(TimeSpan.FromSeconds(300), options.GetSessionIdleTimeout());
            options.SessionIdleSeconds = 15;
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.GetSessionIdleTimeout());
            options.SessionIdleSeconds = -1;
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.GetSessionIdleTimeout());
        }

        /// <summary>
        /// 解析 server 时桥接宿主工厂，并把监听配置与后续协议日志送到同一个 provider。
        /// </summary>
        [Test]
        public void HostedServerBridgesLoggerFactoryAndEmitsListenerMessages()
        {
            var recording = new RecordingLoggerProvider();
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(recording));
            services.AddCoapServer(new CoapServerListenOptions { BindAddress = "127.0.0.1", Port = 0 });
            using var provider = services.BuildServiceProvider();

            _ = provider.GetRequiredService<IServer>();
            Assert.AreSame(provider.GetRequiredService<ILoggerFactory>(), CoapLogging.LoggerFactory);
            Assert.IsTrue(recording.Messages.Any(message => message.Contains("CoAP UDP/coap")));
            CoapLogging.CreateLogger<CoapConfigurationHostingTest>().LogInformation("component-bridge-probe");
            Assert.IsTrue(recording.Messages.Any(message => message.Contains("component-bridge-probe")));
        }

        /// <summary>
        /// 重复查询键保留请求顺序，单值辅助方法选择最后一个已解码值。
        /// </summary>
        [Test]
        public void QueryHelpersPreserveRepeatedValuesAndSelectLastValue()
        {
            var context = QueryContext("tag=first", "tag=room%201", "tag=last=segment");
            CollectionAssert.AreEqual(new[] { "first", "room 1", "last=segment" }, context.GetQueryValues("tag"));
            Assert.IsTrue(context.TryGetQueryValue("tag", out var value));
            Assert.AreEqual("last=segment", value);
        }

        /// <summary>
        /// URI 查询只做百分号解码，加号继续表示加号而不是表单空格。
        /// </summary>
        /// <param name="query">原始 URI 查询。</param>
        /// <param name="expected">预期解码结果。</param>
        [TestCase("value=a+b", "a+b")]
        [TestCase("value=a%2Bb", "a+b")]
        [TestCase("value=a%20b", "a b")]
        [TestCase("value=%2520", "%20")]
        [TestCase("value=%ZZ", "%ZZ")]
        [TestCase("value=x%3Dy", "x=y")]
        public void QueryHelpersDecodePercentEncodingWithoutFormUrlSemantics(string query, string expected)
        {
            var context = QueryContext(query);
            Assert.IsTrue(context.TryGetQueryValue("value", out var value));
            Assert.AreEqual(expected, value);
        }

        /// <summary>
        /// 默认按 Ordinal 匹配已解码的键，需要忽略大小写时必须显式指定。
        /// </summary>
        [Test]
        public void QueryHelpersDecodeKeysAndRespectRequestedComparison()
        {
            var context = QueryContext("TaG=mixed", "%74ag=encoded", "TAG=upper");
            CollectionAssert.AreEqual(new[] { "encoded" }, context.GetQueryValues("tag"));
            CollectionAssert.AreEqual(new[] { "mixed", "encoded", "upper" }, context.GetQueryValues("tag", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(context.TryGetQueryValue("tag", StringComparison.OrdinalIgnoreCase, out var value));
            Assert.AreEqual("upper", value);
        }

        /// <summary>
        /// 空值与缺失键的合同不同，带分隔符和不带分隔符的 flag 都表示空值。
        /// </summary>
        [Test]
        public void QueryHelpersDistinguishEmptyValuesFromMissingKeys()
        {
            var context = QueryContext("flag", "flag=");
            CollectionAssert.AreEqual(new[] { "", "" }, context.GetQueryValues("flag"));
            Assert.IsTrue(context.TryGetQueryValue("flag", out var empty));
            Assert.AreEqual(string.Empty, empty);
            Assert.IsFalse(context.TryGetQueryValue("missing", out var missing));
            Assert.IsNull(missing);
            CollectionAssert.IsEmpty(QueryContext().GetQueryValues("missing"));
        }

        /// <summary>
        /// 查询键本身为空时拒绝调用，避免隐式查询全部参数。
        /// </summary>
        /// <param name="name">无效键。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void QueryHelpersRejectMissingNames(string name)
        {
            var context = QueryContext("value=present");
            Assert.Throws<ArgumentException>(() => context.GetQueryValues(name));
            Assert.Throws<ArgumentException>(() => context.TryGetQueryValue(name, out _));
        }

        /// <summary>
        /// 创建只包含通用 CoAP 监听配置的内存 section。
        /// </summary>
        private static IConfigurationSection Configuration(params (string Key, string Value)[] values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(item => "CoapServer:" + item.Key, item => item.Value))
                .Build()
                .GetSection("CoapServer");
        }

        /// <summary>
        /// 为查询辅助方法创建匹配后的通用诊断 resource 上下文。
        /// </summary>
        private static CoapRouteContext QueryContext(params string[] queries)
        {
            var endpoint = new CoapEndpoint(Method.GET, "diagnostics/query",
                _ => new ValueTask<CoapRouteResult>(CoapRouteResult.Changed()));
            return new CoapRouteContext(endpoint, Method.GET,
                new[] { "diagnostics", "query" }, new Dictionary<string, string>(), queries,
                ReadOnlyMemory<byte>.Empty, MediaType.Undefined, MediaType.Undefined);
        }

        private sealed class RecordingLoggerProvider : ILoggerProvider
        {
            public List<string> Messages { get; } = new List<string>();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

            public void Dispose()
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly List<string> _messages;

            public RecordingLogger(List<string> messages) => _messages = messages;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) => _messages.Add(formatter(state, exception));
        }
    }
}
