// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.

namespace WorldDirect.CoAP
{
    using System;

    /// <summary>
    /// The exception that is thrown if an invalid format in a CoAP message was found.
    /// </summary>
    /// <seealso cref="System.ApplicationException" />
    public class MessageFormatErrorException : ApplicationException
    {
        private readonly string partOfMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageFormatErrorException"/> class.
        /// </summary>
        public MessageFormatErrorException()
            : this("A part of the CoAP message was not in a valid format", string.Empty, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageFormatErrorException"/> class.
        /// </summary>
        /// <param name="message">A message that describes the error.</param>
        public MessageFormatErrorException(string message)
            : this(message, string.Empty, null)
        {
        }

        public MessageFormatErrorException(string message, string partOfMessage)
            : base(message, null)
        {
            this.partOfMessage = partOfMessage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageFormatErrorException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="partOfMessage">The part of the CoAP message that caused that error.</param>
        /// <param name="innerException">The inner exception.</param>
        public MessageFormatErrorException(string message, string partOfMessage, Exception innerException)
            : base(message, innerException)
        {
            this.partOfMessage = partOfMessage;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.partOfMessage))
            {
                return this.Message;
            }

            return $"{this.Message} (Causing part of message: {this.partOfMessage}).";
        }
    }
}
