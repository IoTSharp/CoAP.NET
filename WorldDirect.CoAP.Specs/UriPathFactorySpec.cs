namespace WorldDirect.CoAP.Specs
{
    using System.Text;
    using FluentAssertions;
    using V1.Options;
    using Xunit;

    public class UriPathFactorySpec
    {
        private readonly UriPathFactory cut;

        public UriPathFactorySpec()
        {
            this.cut = new UriPathFactory();
        }

        [Fact]
        public void FactoryCanCreateUriPathFromString()
        {
            var expectedUriPath = new UriPath(".well-known");

            var content = Encoding.UTF8.GetBytes(".well-known");
            var optionData = new OptionData(0, 1, (ushort)content.Length, content);
            var actualUriPath = this.cut.Create(optionData);

            actualUriPath.Should().Be(expectedUriPath);
        }
    }
}