namespace WorldDirect.CoAP.Codes.MethodCodes
{
    using ResponseCodes.ClientResponseCodes;
    using ResponseCodes.ServerResponseCodes;
    using ResponseCodes.SuccessfulResponseCodes;

    public static class CoapCodes
    {
        public static class Method
        {
            public static readonly CoapCode GET = new Get();

            public static readonly CoapCode POST = new Post();

            public static readonly CoapCode PUT = new Put();

            public static readonly CoapCode DELETE = new Delete();
        }

        public static class SuccessfulResponse
        {
            public static readonly CoapCode CREATED = new Created();

            public static readonly CoapCode DELETED = new Deleted();

            public static readonly CoapCode VALID = new Valid();

            public static readonly CoapCode CHANGED = new Changed();

            public static readonly CoapCode CONTENT = new Content();
        }

        public static class ClientResponse
        {
            public static readonly CoapCode BAD_REQUEST = new BadRequest();

            public static readonly CoapCode UNAUTHORIZED = new Unauthorized();

            public static readonly CoapCode BAD_OPTION = new BadOption();

            public static readonly CoapCode FORBIDDEN = new Forbidden();

            public static readonly CoapCode NOT_FOUND = new NotFound();

            public static readonly CoapCode METHOD_NOT_ALLOWED = new MethodNotAllowed();

            public static readonly CoapCode NOT_ACCEPTABLE = new NotAcceptable();

            public static readonly CoapCode PRECONDITION_FAILED = new PreconditionFailed();

            public static readonly CoapCode REQUEST_ENTITIY_TOO_LARGE = new RequestEntityTooLarge();

            public static readonly CoapCode UNSUPPORTED_CONTENT_FORMAT = new UnsupportedContentFormat();
        }

        public static class ServerResponse
        {
            public static readonly CoapCode INTERNAL_SERVER_ERROR = new InternalServerError();

            public static readonly CoapCode NOT_IMPLEMENTED = new NotImplemented();

            public static readonly CoapCode BAD_GATEWAY = new BadGateway();

            public static readonly CoapCode SERVICE_UNAVAILABLE = new ServiceUnavailable();

            public static readonly CoapCode GATEWAY_TIMEOUT = new GatewayTimeout();

            public static readonly CoapCode PROXYING_NOT_SUPPORTED = new ProxyingNotSupported();
        }
    }
}