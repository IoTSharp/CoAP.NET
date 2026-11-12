using System;
using System.Text;

namespace CoAP.Net
{
    /// <summary>描述当前端点实际理解的选项及其入站校验规则。</summary>
    public sealed class CoapOptionDefinition
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly Func<Message, Option, Boolean> _validator;

        /// <summary>创建选项定义；注册意味着宿主实际理解该选项，而不只是知道其编号。</summary>
        /// <param name="number">选项编号。</param>
        /// <param name="name">诊断名称。</param>
        /// <param name="format">值的格式。</param>
        /// <param name="minimumLength">最小字节长度。</param>
        /// <param name="maximumLength">最大字节长度。</param>
        /// <param name="requestOption">是否用于请求。</param>
        /// <param name="responseOption">是否用于响应。</param>
        /// <param name="repeatableInRequest">请求中是否可重复。</param>
        /// <param name="repeatableInResponse">响应中是否可重复。</param>
        /// <param name="validator">可选的值和消息上下文校验器；仅在基本校验通过后调用。</param>
        public CoapOptionDefinition(Int32 number, String name, OptionFormat format,
            Int32 minimumLength, Int32 maximumLength, Boolean requestOption, Boolean responseOption,
            Boolean repeatableInRequest = false, Boolean repeatableInResponse = false,
            Func<Message, Option, Boolean> validator = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(number);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(number, UInt16.MaxValue);
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, minimumLength);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumLength, 65804);
            if (format == OptionFormat.Integer && maximumLength > sizeof(UInt64))
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            if (!requestOption && !responseOption)
                throw new ArgumentException("The option must apply to requests or responses.");

            Number = number;
            Name = name;
            Format = format;
            MinimumLength = minimumLength;
            MaximumLength = maximumLength;
            RequestOption = requestOption;
            ResponseOption = responseOption;
            RepeatableInRequest = repeatableInRequest;
            RepeatableInResponse = repeatableInResponse;
            _validator = validator;
        }

        /// <summary>选项编号。</summary>
        public Int32 Number { get; }
        /// <summary>诊断名称。</summary>
        public String Name { get; }
        /// <summary>值的格式。</summary>
        public OptionFormat Format { get; }
        /// <summary>最小字节长度。</summary>
        public Int32 MinimumLength { get; }
        /// <summary>最大字节长度。</summary>
        public Int32 MaximumLength { get; }
        /// <summary>是否用于请求。</summary>
        public Boolean RequestOption { get; }
        /// <summary>是否用于响应。</summary>
        public Boolean ResponseOption { get; }
        /// <summary>请求中是否可重复。</summary>
        public Boolean RepeatableInRequest { get; }
        /// <summary>响应中是否可重复。</summary>
        public Boolean RepeatableInResponse { get; }

        internal Boolean IsValid(Message message, Option option, Int32 occurrence)
        {
            if (message.IsRequest ? !RequestOption : !ResponseOption)
                return false;
            if (occurrence > 1 && !(message.IsRequest ? RepeatableInRequest : RepeatableInResponse))
                return false;

            Byte[] value = option.RawValue ?? Array.Empty<Byte>();
            if (value.Length < MinimumLength || value.Length > MaximumLength)
                return false;
            if (Format == OptionFormat.String)
            {
                try
                {
                    StrictUtf8.GetCharCount(value);
                }
                catch (DecoderFallbackException)
                {
                    return false;
                }
            }
            return _validator == null || _validator(message, option);
        }

        internal static UInt64 ReadUnsigned(Option option)
        {
            UInt64 result = 0;
            foreach (Byte value in option.RawValue ?? Array.Empty<Byte>())
                result = (result << 8) | value;
            return result;
        }
    }
}
