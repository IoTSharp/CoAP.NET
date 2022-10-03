// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Specs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FakeItEasy;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Oscore;
    using WorldDirect.CoAP.Codes;
    using WorldDirect.CoAP.Codes.MethodCodes;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.Common.Extensions;
    using WorldDirect.CoAP.V1;
    using WorldDirect.CoAP.V1.Messages;
    using WorldDirect.CoAP.V1.Options;
    using Xunit;

    /// <summary>
    /// Provides all specs that are related to <see cref="CoapMessageSerializer"/>.
    /// </summary>
    public class CoapMessageV1SerializerSpecs
    {
        private readonly CoapMessageSerializer cut;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoapMessageV1SerializerSpecs"/> class.
        /// </summary>
        public CoapMessageV1SerializerSpecs()
        {
            var factories = CoapMessageV1SerializerSpecs.GetOptionFactories();
            var codes = CoapMessageV1SerializerSpecs.GetCoapCodes();
            var logger = A.Fake<ILogger<CoapMessageSerializer>>();
            this.cut = new CoapMessageSerializer(new HeaderReader(new CodeRegistry(codes)), new TokenReader(), new OptionsReader(factories), new PayloadReader(), logger);
        }

        /// <summary>
        /// The deserializer can deserialize a message with payload.
        /// </summary>
        [Fact]
        public void TheDeserializerCanDeserializeAMessageWithPayload()
        {
            var bytes = ByteArrayExtensions.FromHexString("44 01 2e df 4f f1 91 11 b3 61 62 63 03 64 65 66 03 67 68 69 ff 0c 0b 0a");
            var options = new List<CoapOption>()
            {
                new UriPath("abc"),
                new UriPath("def"),
                new UriPath("ghi"),
            };
            var payload = new byte[] { 0x0A, 0x0B, 0x0C };
            var header = new CoapHeader(CoapVersion.V1, CoapType.Confirmable, new CoapTokenLength((UInt4)4), CoapCodes.Method.GET, (CoapMessageId)11999);
            var expectedMessage = new CoapMessage(header, new CoapToken(0x4ff19111, new CoapTokenLength((UInt4)4)), options, payload);

            var message = this.cut.Deserialize(bytes);
            message.Should().Be(expectedMessage);
        }

        /// <summary>
        /// The deserializer can deserialize a message without payload.
        /// </summary>
        [Fact]
        public void DeserializeMessageWithoutPayload()
        {
            var bytes = ByteArrayExtensions.FromHexString("44 01 2e df 4f f1 91 11 b3 61 62 63 03 64 65 66 03 67 68 69");
            var options = new List<CoapOption>()
            {
                new UriPath("abc"),
                new UriPath("def"),
                new UriPath("ghi"),
            };
            var header = new CoapHeader(CoapVersion.V1, CoapType.Confirmable, new CoapTokenLength((UInt4)4), CoapCodes.Method.GET, (CoapMessageId)11999);
            var expectedMessage = new CoapMessage(header, new CoapToken(0x4ff19111, new CoapTokenLength((UInt4)4)), options, new byte[0]);

            var message = this.cut.Deserialize(bytes);
            message.Should().Be(expectedMessage);
        }

        /// <summary>
        /// The deserializer throws a <see cref="MessageFormatErrorException"/> if a payload marker without a payload is given.
        /// </summary>
        [Fact]
        public void DeserializerThrowsMessageFormatErrorExceptionIfPayloadMarkerWithoutAPayloadIsGiven()
        {
            var bytes = new byte[]
            {
                0x54, 0x45, 0xca, 0x3d, 0x8e, 0xef, 0x00, 0x00, 0x15, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x05, 0x56, 0x61, 0x6C, 0x75, 0x65, 0xFF,
            };

            Assert.Throws<MessageFormatErrorException>(() => this.cut.Deserialize(bytes));
        }

        [Fact]
        public void LwM2MRegistrationRequest()
        {
            const string inputString = "44 02 00 00 00 00 38 00 b2 72 64 11 28 39 6c 77 " +
                                       "6d 32 6d 3d 31 2e 31 0d 09 65 70 3d 50 4f 31 4f " +
                                       "44 41 32 4e 6a 49 34 55 51 67 41 51 51 41 37 03 " +
                                       "62 3d 55 05 6c 74 3d 39 30 ff 3c 2f 3e 3b 72 74 " +
                                       "3d 22 6f 6d 61 2e 6c 77 6d 32 6d 22 3b 63 74 3d " +
                                       "31 31 35 34 33 2c 3c 2f 31 2f 30 3e 2c 3c 2f 33 " +
                                       "2f 30 3e 2c 3c 2f 34 2f 30 3e 2c 3c 2f 35 2f 30 " +
                                       "3e 2c 3c 2f 37 2f 30 3e 2c 3c 2f 33 32 30 31 2f " +
                                       "30 3e 2c 3c 2f 33 32 30 31 2f 31 3e 2c 3c 2f 33 " +
                                       "33 30 33 2f 30 3e 2c 3c 2f 33 33 30 33 2f 31 3e " +
                                       "2c 3c 2f 33 33 30 33 2f 32 3e 2c 3c 2f 33 33 30 " +
                                       "33 2f 33 3e 2c 3c 2f 33 33 30 33 2f 34 3e 2c 3c " +
                                       "2f 33 33 31 36 2f 30 3e 2c 3c 2f 33 33 31 37 2f " +
                                       "30 3e 2c 3c 2f 33 33 31 38 2f 30 3e 2c 3c 2f 33 " +
                                       "33 32 38 2f 30 3e 2c 3c 2f 33 33 32 38 2f 31 3e " +
                                       "2c 3c 2f 33 33 33 31 2f 30 3e 2c 3c 2f 31 30 33 " +
                                       "30 30 2f 30 3e 2c 3c 2f 31 30 33 30 30 2f 31 3e " +
                                       "2c 3c 2f 31 30 33 30 30 2f 32 3e 2c 3c 2f 31 30 " +
                                       "33 30 30 2f 33 3e 2c 3c 2f 31 30 33 30 30 2f 34 " +
                                       "3e 2c 3c 2f 31 30 33 30 30 2f 35 3e 2c 3c 2f 31 " +
                                       "30 33 30 30 2f 36 3e 2c 3c 2f 31 30 33 30 30 2f " +
                                       "37 3e 2c 3c 2f 31 30 33 30 30 2f 38 3e 2c 3c 2f " +
                                       "31 30 33 30 30 2f 39 3e 2c 3c 2f 32 38 31 30 30 " +
                                       "2f 30 3e 2c 3c 2f 32 38 35 30 30 2f 30 3e 2c 3c " +
                                       "2f 32 38 35 30 30 2f 31 3e 2c 3c 2f 32 38 35 30 " +
                                       "30 2f 32 3e 2c 3c 2f 32 38 35 30 30 2f 33 3e 2c " +
                                       "3c 2f 32 38 35 30 30 2f 34 3e 2c 3c 2f 32 38 35 " +
                                       "30 30 2f 35 3e 2c 3c 2f 32 38 35 30 30 2f 36 3e " +
                                       "2c 3c 2f 32 38 35 30 30 2f 37 3e 2c 3c 2f 32 38 " +
                                       "35 30 30 2f 38 3e 2c 3c 2f 32 39 30 34 31 2f 30 " +
                                       "3e 2c 3c 2f 32 39 30 35 31 2f 30 3e";

            var message = this.cut.Deserialize(ByteArrayExtensions.FromHexString(inputString));
        }

        [Fact]
        public void LwM2MRegistrationResponse()
        {
            const string inputString = "64 41 00 00 00 00 38 00 82 72 64 0d 13 62 38 " +
                                       "36 38 39 61 35 64 30 36 61 65 34 35 39 63 62 " +
                                       "33 35 30 39 35 63 66 33 36 65 35 39 64 36 63";

            var message = this.cut.Deserialize(ByteArrayExtensions.FromHexString(inputString));
        }

        [Fact]
        public void X()
        {
            var bytes = new byte[]
            {
                0x54, 0x45, 0xca, 0x3d, 0x8e, 0xef, 0x00, 0x00, 0x95, 0x56, 0x61, 0x6C, 0x75, 0x65, 0x05, 0x56, 0x61, 0x6C, 0x75, 0x65,
            };

            var message = this.cut.Deserialize(bytes);
        }

        private static IEnumerable<CoapCode> GetCoapCodes()
        {
            var codes = new List<CoapCode>()
            {
                CoapCodes.Method.GET,
                CoapCodes.Method.POST,
                CoapCodes.Method.PUT,
                CoapCodes.Method.DELETE,
                CoapCodes.ClientResponse.BAD_OPTION,
                CoapCodes.ClientResponse.BAD_REQUEST,
                CoapCodes.ClientResponse.FORBIDDEN,
                CoapCodes.ClientResponse.METHOD_NOT_ALLOWED,
                CoapCodes.ClientResponse.NOT_ACCEPTABLE,
                CoapCodes.ClientResponse.NOT_FOUND,
                CoapCodes.ClientResponse.PRECONDITION_FAILED,
                CoapCodes.ClientResponse.REQUEST_ENTITIY_TOO_LARGE,
                CoapCodes.ClientResponse.UNAUTHORIZED,
                CoapCodes.ClientResponse.UNSUPPORTED_CONTENT_FORMAT,
                CoapCodes.ServerResponse.BAD_GATEWAY,
                CoapCodes.ServerResponse.GATEWAY_TIMEOUT,
                CoapCodes.ServerResponse.INTERNAL_SERVER_ERROR,
                CoapCodes.ServerResponse.NOT_IMPLEMENTED,
                CoapCodes.ServerResponse.PROXYING_NOT_SUPPORTED,
                CoapCodes.ServerResponse.SERVICE_UNAVAILABLE,
                CoapCodes.SuccessfulResponse.CHANGED,
                CoapCodes.SuccessfulResponse.CONTENT,
                CoapCodes.SuccessfulResponse.CREATED,
                CoapCodes.SuccessfulResponse.DELETED,
                CoapCodes.SuccessfulResponse.VALID,
            };

            return codes;
        }

        private static IEnumerable<IOptionFactory> GetOptionFactories()
        {
            var contentFormats = new List<ContentFormat>()
            {
                ContentFormats.ExiContentFormat,
                ContentFormats.JsonContentFormat,
                ContentFormats.LinkContentFormat,
                ContentFormats.OctetContentFormat,
                ContentFormats.TextPlainContentFormat,
                ContentFormats.XmlContentFormat,
            };
            var contentFormatRegistry = new ContentFormatRegistry(contentFormats);
            var contentFormatFactory = new ContentFormatFactory(contentFormatRegistry);
            var factories = typeof(UriHostFactory)
                .Assembly
                .GetTypes()
                .Where(t => typeof(IOptionFactory).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(ContentFormatFactory))
                .Select(Activator.CreateInstance)
                .Cast<IOptionFactory>()
                .Union(new IOptionFactory[]
                {
                    contentFormatFactory,
                    new OscoreOptionFactory(),
                });
            return factories;
        }
    }
}
