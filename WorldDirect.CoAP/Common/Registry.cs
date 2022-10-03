// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP.Common
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides common functionality to handle, receive and store <typeparamref name="T"/>s in a registry.
    /// </summary>
    /// <typeparam name="T">The elements that are stored in the registry.</typeparam>
    public abstract class Registry<T> : IEnumerable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Registry{T}"/> class.
        /// </summary>
        protected Registry()
        {
            this.Elements = new List<T>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Registry{T}"/> class.
        /// </summary>
        /// <param name="elements">The <typeparamref name="T"/>s that should represents the registry's items.</param>
        /// <remarks>
        /// The default values for the given registry will be overwritten with the specifies <typeparamref name="T"/>s.
        /// </remarks>
        protected Registry(IEnumerable<T> elements)
        {
            this.Elements = new List<T>(elements);
        }

        /// <summary>
        /// Gets or sets the <typeparamref name="T"/>s that are stored in the registry.
        /// </summary>
        /// <value>
        /// The <typeparamref name="T"/>s.
        /// </value>
        protected List<T> Elements { get; set; }

        /// <summary>
        /// Adds the specified <typeparamref name="T"/> to the registry.
        /// </summary>
        /// <param name="element">The <typeparamref name="T"/> that should be added.</param>
        /// <remarks>
        /// If the <typeparamref name="T"/> already exists in the registry, then the method
        /// returns immediately.
        /// </remarks>
        public void Add(T element)
        {
            if (this.Exists(element))
            {
                return;
            }

            this.Elements.Add(element);
        }

        /// <summary>
        /// Removes the specified <typeparamref name="T"/> from the registry.
        /// </summary>
        /// <param name="element">The <typeparamref name="T"/> that is removed.</param>
        /// <returns><c>true</c> if the specified <typeparamref name="T"/> is successfully removed; otherwise <c>false</c>.
        /// This method also return <c>false</c> if the specified <typeparamref name="T"/> was not found in the registry.</returns>
        public bool Remove(T element)
        {
            return this.Elements.Remove(element);
        }

        /// <summary>
        /// Gets the <typeparamref name="T"/> that satisfies a specified predicate.
        /// </summary>
        /// <param name="predicate">A function to test an element for a condition.</param>
        /// <returns>The single <typeparamref name="T"/> that satisfies the condition.</returns>
        /// <exception cref="ArgumentException">Throws if none element satisfies the condition.</exception>
        public T Get(Func<T, bool> predicate)
        {
            var item = this.Elements.SingleOrDefault(predicate);
            if (item == null)
            {
                throw new ArgumentException($"Can not find a single {typeof(T).Name} that matches the given predicate", nameof(predicate));
            }

            return item;
        }

        /// <summary>
        /// Determines whether the registry contains the specified element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><c>true</c> if the registry contains the specified element. Otherwise, <c>false</c>.</returns>
        public bool Exists(T element)
        {
            return this.Elements.Any(c => c.Equals(element));
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.Elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
