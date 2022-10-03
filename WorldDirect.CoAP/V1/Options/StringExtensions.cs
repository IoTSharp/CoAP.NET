namespace WorldDirect.CoAP.V1.Options
{
    using System.Text;

    public static class StringExtensions
    {
        /// <summary>
        /// Insert dashes into the specified <see cref="string"/> where a uppercase letter is. This means that the string "HelloWorld" will be constructed to "Hello-World".
        /// </summary>
        /// <param name="value">The <see cref="string"/> that should be transformed.</param>
        /// <returns>A newly created <seealso cref="string"/> that contains dashes where a uppercase letter is.</returns>
        public static string Dasherize(this string value)
        {
            var builder = new StringBuilder();

            builder.Append(value[0]);
            for (int i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]))
                {
                    builder.Append('-');
                }

                builder.Append(value[i]);
            }

            return builder.ToString();
        }

        public static string Dasherize(this CoapOption option)
        {
            if (option.GetType() == typeof(ETag))
            {
                return "ETag";
            }

            return option.GetType().Name.Dasherize();
        }
    }
}