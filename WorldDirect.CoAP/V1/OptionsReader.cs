// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Codes.ResponseCodes.ServerResponseCodes;
    using WorldDirect.CoAP.Codes.Common;
    using WorldDirect.CoAP.Common;
    using WorldDirect.CoAP.V1.Options;

    public class OptionsReader : IReader<IReadOnlyCollection<CoapOption>>
    {
        private const byte MASK_DELTA = 0xF0;
        private const byte MASK_LENGTH = 0x0F;
        private const byte PAYLOAD_MARKER = 0xFF;

        private readonly Dictionary<int, IOptionFactory> factories;

        public OptionsReader(IEnumerable<IOptionFactory> factories)
        {
            this.factories = factories.ToDictionary(f => f.Number);
        }

        public int Read(ReadOnlyMemory<byte> value, out IReadOnlyCollection<CoapOption> result)
        {
            var options = new List<CoapOption>();
            var offset = 0;
            var previousNumber = 0;
            while (OptionsReader.HasNext(value.Span.Slice(offset)))
            {
                var optionData = OptionsReader.GetOptionData(previousNumber, value.Span.Slice(offset), out var bytesConsumed);
                previousNumber = optionData.Number;
                offset += bytesConsumed;

                var factory = this.factories.ContainsKey(optionData.Number)
                    ? this.factories[optionData.Number]
                    : new UnknownFactory();
                var option = factory.Create(optionData);

                options.Add(option);
            }

            result = new ReadOnlyCollection<CoapOption>(options);
            return offset;
        }

        private static OptionData GetOptionData(int previousNumber, ReadOnlySpan<byte> src, out int bytesConsumed)
        {
            var delta = OptionsReader.ReadDelta(src);
            var length = OptionsReader.ReadLength(src, delta.Size);

            var valueOffset = 1 + delta.Size + length.Size;

            bytesConsumed = valueOffset + length.Value;
            return new OptionData((ushort)previousNumber, delta.Value, length.Value, src.Slice(valueOffset, length.Value));
        }

        private static OptionsLength ReadLength(ReadOnlySpan<byte> value, byte readDeltaBytes)
        {
            var length = (UInt4)(value[0] & MASK_LENGTH);
            var startOfExtendedLength = readDeltaBytes + 1;
            if (length == 15)
            {
                throw new MessageFormatErrorException("Length value of 15 is reserved for future use.");
            }

            if (length == 14)
            {
                var extended = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(value.Slice(startOfExtendedLength, 2)) + 269);
                return new OptionsLength(extended, 2);
            }

            if (length == 13)
            {
                var extended = (ushort)(value[startOfExtendedLength] + 13);
                return new OptionsLength(extended, 1);
            }

            return new OptionsLength(length, 0);
        }

        private static OptionsDelta ReadDelta(ReadOnlySpan<byte> value)
        {
            var delta = (UInt4)((value[0] & MASK_DELTA) >> 4);
            var length = (UInt4)(value[0] & MASK_LENGTH);
            if (delta == 15 && length != 15)
            {
                throw new MessageFormatErrorException("Delta value of 15 is reserved for payload marker.");
            }

            if (delta == 14)
            {
                var extended = (ushort)(BinaryPrimitives.ReadUInt16BigEndian(value.Slice(1, 2)) - 269);
                return new OptionsDelta(extended, 2);
            }

            if (delta == 13)
            {
                var extended = (ushort)(value[1] - 13);
                return new OptionsDelta(extended, 1);
            }

            return new OptionsDelta(delta, 0);
        }

        private static bool HasNext(ReadOnlySpan<byte> value)
        {
            return value.Length != 0 && value[0] != PAYLOAD_MARKER;
        }
    }
}
