/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoAP
{
    /// <summary>
    /// Provides the Microsoft.Extensions.Logging integration point used by CoAP.NET.
    /// </summary>
    public static class CoapLogging
    {
        private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        /// <summary>
        /// Gets or sets the logger factory used by CoAP.NET.
        /// </summary>
        public static ILoggerFactory LoggerFactory
        {
            get { return _loggerFactory; }
            set { _loggerFactory = value ?? NullLoggerFactory.Instance; }
        }

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        public static ILogger CreateLogger(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            return _loggerFactory.CreateLogger(type.FullName ?? type.Name);
        }

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        public static ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }
    }
}
