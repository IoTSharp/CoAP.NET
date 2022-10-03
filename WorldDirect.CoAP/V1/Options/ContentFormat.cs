// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    public abstract class ContentFormat : UIntOptionFormat
    {
        public const ushort NUMBER = 12;

        protected ContentFormat(uint id, string mediaType, IEnumerable<Encoding> encodings)
            : base(NUMBER, id, 0, 2)
        {
            this.MediaType = mediaType;
            this.Encodings = new List<Encoding>(encodings);
        }

        protected ContentFormat(uint id, string mediaType)
            : this(id, mediaType, Enumerable.Empty<Encoding>())
        {
        }

        public string MediaType { get; }

        public IReadOnlyList<Encoding> Encodings { get; }

        public uint Id => this.Value;

        public override string ToString() => $"{this.Name} ({this.Number}): {this.MediaType}";
    }
}
