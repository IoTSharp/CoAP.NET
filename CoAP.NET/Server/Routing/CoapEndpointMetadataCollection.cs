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
using System.Collections;
using System.Collections.Generic;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Ordered metadata attached to a CoAP endpoint.
    /// </summary>
    public sealed class CoapEndpointMetadataCollection : IReadOnlyList<object>
    {
        /// <summary>
        /// Gets a shared empty metadata collection.
        /// </summary>
        public static readonly CoapEndpointMetadataCollection Empty =
            new CoapEndpointMetadataCollection(Array.Empty<object>());

        private readonly object[] _items;

        /// <summary>
        /// Creates an endpoint metadata collection.
        /// </summary>
        /// <param name="items">Metadata items in declaration order.</param>
        public CoapEndpointMetadataCollection(IEnumerable<object> items)
        {
            if (items == null)
            {
                _items = Array.Empty<object>();
                return;
            }

            var list = new List<object>();
            foreach (var item in items)
            {
                if (item != null)
                {
                    list.Add(item);
                }
            }

            _items = list.Count == 0 ? Array.Empty<object>() : list.ToArray();
        }

        /// <summary>
        /// Creates an endpoint metadata collection.
        /// </summary>
        /// <param name="items">Metadata items in declaration order.</param>
        public CoapEndpointMetadataCollection(params object[] items)
            : this((IEnumerable<object>)items)
        {
        }

        /// <inheritdoc />
        public object this[int index] => _items[index];

        /// <inheritdoc />
        public int Count => _items.Length;

        /// <summary>
        /// Gets the most specific metadata item assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The metadata type to find.</typeparam>
        /// <returns>The last matching item, or <c>null</c> when no item is present.</returns>
        public T GetMetadata<T>()
            where T : class
        {
            for (var i = _items.Length - 1; i >= 0; i--)
            {
                if (_items[i] is T value)
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Enumerates metadata items assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The metadata type to find.</typeparam>
        /// <returns>Matching metadata items in declaration order.</returns>
        public IEnumerable<T> OfType<T>()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i] is T value)
                {
                    yield return value;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<object> GetEnumerator()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                yield return _items[i];
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
