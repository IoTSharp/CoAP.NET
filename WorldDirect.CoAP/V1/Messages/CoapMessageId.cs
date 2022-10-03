namespace WorldDirect.CoAP.V1.Messages
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public struct CoapMessageId : IEquatable<CoapMessageId>
    {
        /// <summary>
        /// The default value of a <see cref="CoapMessageId"/>. It represents a <see cref="CoapMessageId"/> with value 0.
        /// </summary>
        public static readonly CoapMessageId Default = new CoapMessageId(0);

        private static readonly Random Random = new Random();

        // This mutex protects the access to the Random field.
        private static readonly SemaphoreSlim Mutex = new SemaphoreSlim(1);
        private readonly ushort value;

        public CoapMessageId(ushort value)
        {
            this.value = value;
        }

        public static explicit operator CoapMessageId(ushort value) => new CoapMessageId(value);

        public static implicit operator ushort(CoapMessageId messageId) => messageId.value;

        public static bool operator ==(CoapMessageId left, CoapMessageId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CoapMessageId left, CoapMessageId right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoapMessageId"/> structure.
        /// </summary>
        /// <param name="ct">The <see cref="CancellationToken"/> that observe that asynchronous initialization of a new <see cref="CoapMessageId"/>.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous initialization of a new <see cref="CoapMessageId"/>.
        /// The result of that <see cref="Task{TResult}"/> contains the new generated <see cref="CoapMessageId"/>.</returns>
        /// <remarks>
        /// This method is thread-safe and uses a <see cref="SemaphoreSlim"/> to protect the access to the <see cref="System.Random"/> instance
        /// of the <see cref="CoapMessageId"/>.
        /// </remarks>
        public static async Task<CoapMessageId> NewMessageIdAsync(CancellationToken ct = default)
        {
            ushort value;

            await Mutex.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                value = (ushort)Random.Next(ushort.MinValue, ushort.MaxValue + 1);
            }
            finally
            {
                Mutex.Release();
            }

            var messageId = new CoapMessageId(value);
            return messageId;
        }

        public bool Equals(CoapMessageId other)
        {
            return this.value == other.value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return this.Equals((CoapMessageId)obj);
        }

        public override int GetHashCode()
        {
            return this.value.GetHashCode();
        }

        public override string ToString()
        {
            return this.value.ToString("D");
        }
    }
}
