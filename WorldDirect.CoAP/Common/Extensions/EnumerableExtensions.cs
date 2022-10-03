// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common.Extensions
{
    using System;
    using System.Collections.Generic;

    public static class EnumerableExtensions
    {
        public static SortedList<TKey, TValue> ToSortedList<TSource, TKey, TValue>(this IEnumerable<TSource> value, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
        {
            var sortedList = new SortedList<TKey, TValue>();
            foreach (var item in value)
            {
                sortedList.Add(keySelector(item), valueSelector(item));
            }

            return sortedList;
        }

        public static SortedList<TKey, TSource> ToSortedList<TSource, TKey>(this IEnumerable<TSource> value, Func<TSource, TKey> keySelector)
        {
            return value.ToSortedList(keySelector, i => i);
        }
    }
}