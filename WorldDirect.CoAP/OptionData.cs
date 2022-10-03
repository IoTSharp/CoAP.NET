// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP
{
    using System;
    using System.Buffers.Binary;
    using System.Linq;
    using System.Text;
    using Common.Extensions;

    public ref struct OptionData
    {
        private readonly ReadOnlySpan<byte> value;

        public OptionData(ushort offset, ushort delta, ushort length, ReadOnlySpan<byte> value)
        {
            this.Number = (ushort)(offset + delta);
            this.Length = length;
            this.value = value;
        }

        public ushort Number { get; }

        public ushort Length { get; }

        /// <summary>
        /// Gets the value of the option.
        /// </summary>
        /// <value>
        /// The value of the option in big endian order.
        /// </value>
        public byte[] Value => BitConverter.IsLittleEndian ? this.value.Reverse().ToArray() : this.value.ToArray();

        public string StringValue => Encoding.UTF8.GetString(this.value.ToArray());

        public uint UIntValue => BinaryPrimitives.ReadUInt32BigEndian(this.value.Align(32));
    }
}
