/*
 * Copyright (c) 2011-2015, Longxiang He <helongxiang@smeshlink.com>,
 * SmeshLink Technology Co.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using System;
using System.Collections.Generic;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Read-only route value dictionary backed by small arrays instead of a hash table.
    /// </summary>
    internal sealed class CoapRouteValueCollection : IReadOnlyDictionary<string, string>
    {
        internal static readonly CoapRouteValueCollection Empty =
            new CoapRouteValueCollection(Array.Empty<string>(), Array.Empty<string>());

        private readonly string[] _keys;
        private readonly string[] _values;

        internal CoapRouteValueCollection(string[] keys, string[] values)
        {
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _values = values ?? throw new ArgumentNullException(nameof(values));
            if (_keys.Length != _values.Length)
            {
                throw new ArgumentException("Route value key and value arrays must have the same length.", nameof(values));
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> Keys => EnumerateKeys();

        /// <inheritdoc />
        public IEnumerable<string> Values => EnumerateValues();

        /// <inheritdoc />
        public int Count => _keys.Length;

        /// <inheritdoc />
        public string this[string key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException("The route value was not found.");
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return IndexOfKey(key) >= 0;
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, out string value)
        {
            var index = IndexOfKey(key);
            if (index >= 0)
            {
                value = _values[index];
                return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                yield return new KeyValuePair<string, string>(_keys[i], _values[i]);
            }
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private IEnumerable<string> EnumerateKeys()
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                yield return _keys[i];
            }
        }

        private IEnumerable<string> EnumerateValues()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                yield return _values[i];
            }
        }

        private int IndexOfKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            for (var i = 0; i < _keys.Length; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
