using System;
using System.Collections.Generic;
using System.Threading;

namespace CoAP.Net
{
    /// <summary>为单个端点注册实际支持的选项；原始解码器不使用此语义注册表。</summary>
    public sealed class CoapOptionRegistry
    {
        private readonly Object _sync = new Object();
        private Dictionary<Int32, CoapOptionDefinition> _definitions = new Dictionary<Int32, CoapOptionDefinition>();

        /// <summary>创建含核心、Observe 和 UDP Blockwise 选项定义的注册表。</summary>
        public CoapOptionRegistry()
        {
            Add(new CoapOptionDefinition(1, "If-Match", OptionFormat.Opaque, 0, 8, true, false, true));
            Add(new CoapOptionDefinition(3, "Uri-Host", OptionFormat.String, 1, 255, true, false));
            Add(new CoapOptionDefinition(4, "ETag", OptionFormat.Opaque, 1, 8, true, true, true,
                validator: (message, option) => !message.IsRequest || ((Request)message).Method == Method.GET));
            Add(new CoapOptionDefinition(5, "If-None-Match", OptionFormat.Opaque, 0, 0, true, false));
            Add(new CoapOptionDefinition(6, "Observe", OptionFormat.Integer, 0, 3, true, true,
                validator: (message, option) => message.IsRequest
                    ? ((Request)message).Method == Method.GET && CoapOptionDefinition.ReadUnsigned(option) <= 1
                    : Code.IsSuccess(message.Code)));
            Add(new CoapOptionDefinition(7, "Uri-Port", OptionFormat.Integer, 0, 2, true, false));
            Add(new CoapOptionDefinition(8, "Location-Path", OptionFormat.String, 0, 255, false, true, false, true,
                validator: (message, option) => option.StringValue != "." && option.StringValue != ".."));
            Add(new CoapOptionDefinition(11, "Uri-Path", OptionFormat.String, 0, 255, true, false, true,
                validator: (message, option) => option.StringValue != "." && option.StringValue != ".."));
            Add(new CoapOptionDefinition(12, "Content-Format", OptionFormat.Integer, 0, 2, true, true));
            Add(new CoapOptionDefinition(14, "Max-Age", OptionFormat.Integer, 0, 4, false, true));
            Add(new CoapOptionDefinition(15, "Uri-Query", OptionFormat.String, 0, 255, true, false, true));
            Add(new CoapOptionDefinition(17, "Accept", OptionFormat.Integer, 0, 2, true, false));
            Add(new CoapOptionDefinition(20, "Location-Query", OptionFormat.String, 0, 255, false, true, false, true));
            Add(new CoapOptionDefinition(23, "Block2", OptionFormat.Integer, 0, 3, true, true,
                validator: (message, option) => (CoapOptionDefinition.ReadUnsigned(option) & 7) != 7));
            Add(new CoapOptionDefinition(27, "Block1", OptionFormat.Integer, 0, 3, true, true,
                validator: (message, option) => (CoapOptionDefinition.ReadUnsigned(option) & 7) != 7));
            Add(new CoapOptionDefinition(28, "Size2", OptionFormat.Integer, 0, 4, true, true,
                validator: (message, option) => !message.IsRequest || CoapOptionDefinition.ReadUnsigned(option) == 0));
            Add(new CoapOptionDefinition(35, "Proxy-Uri", OptionFormat.String, 1, 1034, true, false,
                validator: (message, option) => Uri.TryCreate(option.StringValue, UriKind.Absolute, out Uri uri)
                    && uri.Fragment.Length == 0));
            Add(new CoapOptionDefinition(39, "Proxy-Scheme", OptionFormat.String, 1, 255, true, false,
                validator: (message, option) => Uri.CheckSchemeName(option.StringValue)));
            Add(new CoapOptionDefinition(60, "Size1", OptionFormat.Integer, 0, 4, true, true,
                validator: (message, option) => message.IsRequest || message.Code == Code.RequestEntityTooLarge));
        }

        /// <summary>注册宿主实际处理的自定义选项；不能覆盖已有协议定义。</summary>
        /// <param name="definition">不可变的选项定义。</param>
        public void Register(CoapOptionDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            lock (_sync)
            {
                Dictionary<Int32, CoapOptionDefinition> next = new Dictionary<Int32, CoapOptionDefinition>(_definitions);
                next.Add(definition.Number, definition);
                Volatile.Write(ref _definitions, next);
            }
        }

        private void Add(CoapOptionDefinition definition)
        {
            _definitions.Add(definition.Number, definition);
        }

        internal Boolean ValidateAndFilter(Message message, out String diagnostic)
        {
            Dictionary<Int32, CoapOptionDefinition> definitions = Volatile.Read(ref _definitions);
            Dictionary<OptionType, Int32> occurrences = new Dictionary<OptionType, Int32>();
            Dictionary<OptionType, List<Option>> retained = new Dictionary<OptionType, List<Option>>();
            HashSet<OptionType> changed = new HashSet<OptionType>();
            foreach (Option option in message.GetOptions())
            {
                occurrences.TryGetValue(option.Type, out Int32 count);
                occurrences[option.Type] = ++count;
                Boolean known = definitions.TryGetValue((Int32)option.Type, out CoapOptionDefinition definition);
                Boolean valid = known && definition.IsValid(message, option, count);
                if (!valid && Option.IsCritical(option.Type))
                {
                    diagnostic = "Unrecognized or invalid option " + (Int32)option.Type;
                    return false;
                }
                if (known && !valid)
                {
                    changed.Add(option.Type);
                    continue;
                }
                if (!retained.TryGetValue(option.Type, out List<Option> options))
                {
                    options = new List<Option>();
                    retained.Add(option.Type, options);
                }
                options.Add(option);
            }

            // Proxy-Uri is an alternative to all Uri-* and Proxy-Scheme options.
            if (message.HasOption(OptionType.ProxyUri) &&
                (message.HasOption(OptionType.ProxyScheme) || message.HasOption(OptionType.UriHost) ||
                    message.HasOption(OptionType.UriPort) || message.HasOption(OptionType.UriPath) ||
                    message.HasOption(OptionType.UriQuery)))
            {
                diagnostic = "Proxy-Uri conflicts with Uri-* or Proxy-Scheme";
                return false;
            }

            foreach (OptionType type in changed)
            {
                message.RemoveOptions(type);
                if (retained.TryGetValue(type, out List<Option> options))
                    message.AddOptions(options);
            }
            diagnostic = null;
            return true;
        }
    }
}
