/*
 * Copyright (c) 2026, IoTSharp.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY.
 *
 * This file is part of the CoAP.NET, a CoAP framework in C#.
 * Please see README for more information.
 */

using CoAP.Observe;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CoAP.Server.Routing
{
    /// <summary>
    /// Tracks Observe relations established through route-backed CoAP resources.
    /// </summary>
    public sealed class CoapRouteObserveRegistry
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ObserveRelation>> _relations =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, ObserveRelation>>(StringComparer.Ordinal);

        /// <summary>
        /// Adds or replaces an Observe relation for the supplied route resource path.
        /// </summary>
        /// <param name="resourcePath">The route resource path without the URI scheme or host.</param>
        /// <param name="relation">The relation created by the CoAP observe manager.</param>
        public void AddObserveRelation(string resourcePath, ObserveRelation relation)
        {
            if (relation == null)
            {
                throw new ArgumentNullException(nameof(relation));
            }

            string key = NormalizeResourcePath(resourcePath);
            var bucket = _relations.GetOrAdd(
                key,
                _ => new ConcurrentDictionary<string, ObserveRelation>(StringComparer.Ordinal));

            ObserveRelation old = null;
            bucket.AddOrUpdate(
                relation.Key,
                relation,
                (_, existing) =>
                {
                    old = existing;
                    return relation;
                });

            if (old != null && !ReferenceEquals(old, relation))
            {
                old.Cancel();
            }
        }

        /// <summary>
        /// Removes an Observe relation from the supplied route resource path.
        /// </summary>
        /// <param name="resourcePath">The route resource path without the URI scheme or host.</param>
        /// <param name="relation">The relation to remove.</param>
        public void RemoveObserveRelation(string resourcePath, ObserveRelation relation)
        {
            if (relation == null)
            {
                return;
            }

            string key = NormalizeResourcePath(resourcePath);
            if (!_relations.TryGetValue(key, out var bucket))
            {
                return;
            }

            ((ICollection<KeyValuePair<string, ObserveRelation>>)bucket)
                .Remove(new KeyValuePair<string, ObserveRelation>(relation.Key, relation));

            if (bucket.IsEmpty)
            {
                _relations.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Notifies all established Observe relations for the supplied route resource path.
        /// </summary>
        /// <param name="resourcePath">The route resource path without the URI scheme or host.</param>
        /// <returns>The number of relations scheduled or invoked.</returns>
        public int NotifyObservers(string resourcePath)
        {
            string key = NormalizeResourcePath(resourcePath);
            if (!_relations.TryGetValue(key, out var bucket) || bucket.IsEmpty)
            {
                return 0;
            }

            var relations = bucket.Values.ToArray();
            for (var i = 0; i < relations.Length; i++)
            {
                relations[i].NotifyObservers();
            }

            return relations.Length;
        }

        /// <summary>
        /// Returns the current Observe relation keys for the supplied route resource path.
        /// </summary>
        /// <param name="resourcePath">The route resource path without the URI scheme or host.</param>
        /// <returns>A snapshot of relation keys.</returns>
        public IReadOnlyList<string> GetObserverKeys(string resourcePath)
        {
            string key = NormalizeResourcePath(resourcePath);
            return _relations.TryGetValue(key, out var bucket) && !bucket.IsEmpty
                ? bucket.Keys.ToArray()
                : Array.Empty<string>();
        }

        /// <summary>
        /// Checks whether the supplied route resource path currently has observers.
        /// </summary>
        /// <param name="resourcePath">The route resource path without the URI scheme or host.</param>
        /// <returns><c>true</c> when at least one relation is active.</returns>
        public bool HasObservers(string resourcePath)
        {
            string key = NormalizeResourcePath(resourcePath);
            return _relations.TryGetValue(key, out var bucket) && !bucket.IsEmpty;
        }

        /// <summary>
        /// Normalizes a route resource path to the registry key format.
        /// </summary>
        /// <param name="resourcePath">A route resource path.</param>
        /// <returns>The normalized path.</returns>
        public static string NormalizeResourcePath(string resourcePath)
            => string.IsNullOrWhiteSpace(resourcePath)
                ? string.Empty
                : resourcePath.Trim().Trim('/');
    }
}
