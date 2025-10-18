namespace UglyToad.PdfPig.Tokens
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A token containing string data where the string is encoded as hexadecimal.
    /// </summary>
    public sealed class HexToken : IDataToken<string>
    {
        /// <summary>
        /// The string contained in the hex data.
        /// </summary>
        public string Data { get; }

        private readonly byte[] bytes;

        /// <summary>
        /// The bytes of the hex data.
        /// </summary>
        public ReadOnlySpan<byte> Bytes => bytes;

        /// <summary>
        /// The memory of the hex data.
        /// </summary>
        public ReadOnlyMemory<byte> Memory => bytes;

        /// <summary>
        /// Create a new <see cref="HexToken"/> from the provided hex characters.
        /// </summary>
        /// <param name="characters">A set of hex characters 0-9, A - F, a - f representing a string.</param>
        public HexToken(ReadOnlySpan<char> characters)
        {
            // if the final character is missing, it is considered to be a 0, as per 7.3.4.3
            // adding 1 to the characters array length ensure the size of the byte array is correct
            // in all situations
            var bytes = new byte[(characters.Length+1) / 2];
            int index = 0;

            for (var i = 0; i < characters.Length; i += 2)
            {
                char high = characters[i];
                char low;
                if (i == characters.Length - 1)
                {
                    low = '0';
                }
                else
                {
                    low = characters[i + 1];
                }

                var b = ConvertPair(high, low);
                bytes[index++] = b;
            }

            // Handle UTF-16BE format strings.
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                Data = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
            else
            {
                var builder = new StringBuilder(bytes.Length);

                foreach (var b in bytes)
                {
                    if (b != '\0')
                    {
                        builder.Append((char)b);
                    }
                }

                Data = builder.ToString();
            }

            this.bytes = bytes;
        }

        /// <summary>
        /// Create a new <see cref="HexToken"/> from the provided hex characters.
        /// </summary>
        /// <param name="characters">A set of hex characters 0-9, A - F, a - f representing a string.</param>
        public HexToken(ReadOnlySpan<byte> characters)
        {
            // if the final character is missing, it is considered to be a 0, as per 7.3.4.3
            // adding 1 to the characters array length ensure the size of the byte array is correct
            // in all situations
            var bytes = new byte[(characters.Length + 1) / 2];
            int index = 0;

            for (var i = 0; i < characters.Length; i += 2)
            {
                byte high = characters[i];
                byte low;
                if (i == characters.Length - 1)
                {
                    low = (byte)'0';
                }
                else
                {
                    low = characters[i + 1];
                }

                var b = ConvertPair(high, low);
                bytes[index++] = b;
            }

            // Handle UTF-16BE format strings.
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                Data = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
            else
            {
                var builder = new StringBuilder(bytes.Length);

                foreach (var b in bytes)
                {
                    if (b != '\0')
                    {
                        builder.Append((char)b);
                    }
                }

                Data = builder.ToString();
            }

            this.bytes = bytes;
        }

        /// <summary>
        /// Convert two hex characters to a byte.
        /// </summary>
        /// <param name="high">The high nibble.</param>
        /// <param name="low">The low nibble.</param>
        /// <returns>The byte.</returns>
        public static byte ConvertPair(int high, int low)
        {
            high = high <= '9' ? high - '0' : ((high & 0xF) + 9);
            low = low <= '9' ? low - '0' : ((low & 0xF) + 9);

            return (byte)(high << 4 | low);
        }

        /// <summary>
        /// Convert the bytes in this hex token to an integer.
        /// </summary>
        /// <param name="token">The token containing the data to convert.</param>
        /// <returns>The integer corresponding to the bytes.</returns>
        public static int ConvertHexBytesToInt(HexToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var bytes = token.Bytes;

            var value = bytes[0] & 0xFF;
            if (bytes.Length == 2)
            {
                value <<= 8;
                value += bytes[1] & 0xFF;
            }

            return value;
        }

        /// <inheritdoc />
        public bool Equals(IToken obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (!(obj is HexToken other))
            {
                return false;
            }

            return Data == other.Data;
        }

        /// <summary>
        /// Converts the binary data back to a hex string representation.
        /// </summary>
        public string GetHexString()
        {
#if NET8_0_OR_GREATER
            return Convert.ToHexString(Bytes);
#else
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif
        }
    }
}