using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CoAP.Server.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace CoAP
{
    [TestFixture]
    public sealed class UnsignedOptionHostingTest
    {
        [TestCase(false, 0U)]
        [TestCase(false, 2147483647U)]
        [TestCase(false, 2147483648U)]
        [TestCase(false, UInt32.MaxValue)]
        [TestCase(true, 0U)]
        [TestCase(true, 2147483647U)]
        [TestCase(true, 2147483648U)]
        [TestCase(true, UInt32.MaxValue)]
        public async Task ResourceActionReceivesFullUnsignedSize1Range(bool generated, uint value)
        {
            Request request = Request.NewPost();
            request.ID = 1;
            request.Token = Array.Empty<byte>();
            request.SetBlock1(0, false, 0);
            request.AddOption(Option.Create(OptionType.Size1, value));
            // A Size2 request asks for an estimate and MUST have value zero.
            request.AddOption(Option.Create(OptionType.Size2, 0U));
            Request decoded = (Request)Spec.Decode(Spec.Encode(request));

            string result = await Invoke(generated, decoded.GetOptions().ToArray());

            string expected = value.ToString(CultureInfo.InvariantCulture);
            Assert.That(result, Is.EqualTo(expected + "/0"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task MissingSizesRemainNullable(bool generated)
        {
            Assert.That(await Invoke(generated, Array.Empty<Option>()), Is.EqualTo("/"));
        }

        private static async Task<string> Invoke(bool generated, Option[] options)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddCoapResources(configuration =>
            {
                if (generated)
                    configuration.AddEndpointFactory(global::MyGeneratedCoapEndpoints.Create);
                else
                    configuration.AddReflectionResourceDiscovery();
            });
            using ServiceProvider provider = services.BuildServiceProvider();
            CoapEndpoint endpoint = provider.GetRequiredService<ICoapEndpointDataSource>().Endpoints
                .Single(candidate => candidate.RoutePattern.Template == "unsigned-size");
            CoapRouteContext context = new CoapRouteContext(endpoint, Method.POST,
                new[] { "unsigned-size" }, null, Array.Empty<string>(), ReadOnlyMemory<byte>.Empty,
                MediaType.Undefined, MediaType.Undefined, provider, options: options);
            ICoapResult result = await provider.GetRequiredService<CoapActionInvoker>()
                .InvokeAsync(new CoapActionInvocationContext(context, provider));
            return System.Text.Encoding.UTF8.GetString(result.Payload.Span);
        }
    }

    [CoapResource]
    [CoapRoute("unsigned-size")]
    internal sealed class UnsignedSizeCoapResource
    {
        [CoapPost]
        public CoapRouteResult ReadSizes(
            [CoapFromOption(OptionType.Size1)] uint? size1,
            [CoapFromOption(OptionType.Size2)] uint? size2)
        {
            return CoapRouteResult.Text(StatusCode.Content,
                size1?.ToString(CultureInfo.InvariantCulture) + "/" + size2?.ToString(CultureInfo.InvariantCulture));
        }
    }
}
