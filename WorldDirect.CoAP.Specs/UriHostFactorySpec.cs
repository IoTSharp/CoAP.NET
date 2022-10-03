// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System.Text;
    using FluentAssertions;
    using WorldDirect.CoAP.V1.Options;
    using Xunit;

    public class UriHostFactorySpec
    {
        private readonly UriHostFactory cut;

        public UriHostFactorySpec()
        {
            this.cut = new UriHostFactory();
        }

        [Fact]
        public void FactoryCanCreateUriHostFromString()
        {
            var expectedUriHost = new UriHost("example.net");

            var content = Encoding.UTF8.GetBytes("example.net");
            var optionData = new OptionData(0, 1, (ushort)content.Length, content);
            var actualUriHost = this.cut.Create(optionData);

            actualUriHost.Should().Be(expectedUriHost);
        }
    }
}
