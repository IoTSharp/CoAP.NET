// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.V1.Options
{
    using System.Collections.Generic;
    using WorldDirect.CoAP.Common;

    public class ContentFormatRegistry : Registry<ContentFormat>
    {
        public ContentFormatRegistry(IEnumerable<ContentFormat> contentFormats)
            : base(contentFormats)
        {
        }
    }
}
