/*
 * Copyright (c) 2011-2013, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 * 
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

namespace CoAP
{
    /// <summary>
    /// CoAP option types as defined in
    /// RFC 7252, Section 12.2 and other CoAP extensions.
    /// </summary>
    public enum OptionType
    {
        Unknown = -1,

        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Reserved = 0,
        /// <summary>
        /// C, opaque, 0-8 B, -
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        IfMatch = 1,
        /// <summary>
        /// C, String, 1-270 B, ""
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        UriHost = 3,
        /// <summary>
        /// E, sequence of bytes, 1-4 B, -
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        ETag = 4,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        IfNoneMatch = 5,
        /// <summary>
        /// C, uint, 0-2 B
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        UriPort = 7,
        /// <summary>
        /// E, String, 1-270 B, -
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        LocationPath = 8,
        /// <summary>
        /// C, String, 1-270 B, ""
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        UriPath = 11,
        /// <summary>
        /// C, 8-bit uint, 1 B, 0 (text/plain)
        /// <seealso cref="ContentFormat"/>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        ContentType = 12,
        /// <summary>
        /// C, 8-bit uint, 1 B, 0 (text/plain)
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        ContentFormat = 12,
        /// <summary>
        /// E, variable length, 1--4 B, 60 Seconds
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        MaxAge = 14,
        /// <summary>
        /// C, String, 1-270 B, ""
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        UriQuery = 15,
        /// <summary>
        /// C, Sequence of Bytes, 1-n B, -
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Accept = 17,
        /// <summary>
        /// RFC 9177 的 Q-Block1 option number；仅定义 wire registry，不表示已实现 Q-Block 传输。
        /// </summary>
        QBlock1 = 19,
        /// <summary>
        /// 旧草案 Token option 的兼容名称；现行数字 19 表示 Q-Block1，报文头 Token 使用 <see cref="Message.Token"/>。
        /// </summary>
        [System.Obsolete("旧草案 Token option 已弃用。请使用 Message.Token 设置报文头 Token；现行 option 19 为 Q-Block1。")]
        Token = 19,
        /// <summary>
        /// E, String, 1-270 B, -
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        LocationQuery = 20,
        /// <summary>
        /// C, String, 1-270 B, "coap"
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        ProxyUri = 35,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        ProxyScheme = 39,

        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Size1 = 60,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Reserved1 = 128,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Reserved2 = 132,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Reserved3 = 136,
        /// <summary>
        /// <remarks>RFC 7252</remarks>
        /// </summary>
        Reserved4 = 140,

        /// <summary>
        /// E, Duration, 1 B, 0
        /// <remarks>draft-ietf-core-observe</remarks>
        /// </summary>
        Observe = 6,

        /// <summary>
        /// <remarks>draft-ietf-core-block</remarks>
        /// </summary>
        Block2 = 23,
        /// <summary>
        /// <remarks>draft-ietf-core-block</remarks>
        /// </summary>
        Block1 = 27,
        /// <summary>
        /// RFC 9177 的 Q-Block2 option number；仅定义 wire registry，不表示已实现 Q-Block 传输。
        /// </summary>
        QBlock2 = 31,
        /// <summary>
        /// <remarks>draft-ietf-core-block</remarks>
        /// </summary>
        Size2 = 28,

        /// <summary>
        /// no-op for fenceposting
        /// <remarks>draft-bormann-coap-misc-04</remarks>
        /// </summary>
        [System.Obsolete]
        FencepostDivisor = 114,
    }

    /// <summary>
    /// CoAP option formats
    /// </summary>
    public enum OptionFormat
    {
        Integer,
        String,
        Opaque,
        Unknown
    }
}
