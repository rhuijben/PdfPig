namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using System.Diagnostics.CodeAnalysis;
    using Tokens;

    internal sealed class HexTokenizer : InputByteTokenizer
    {

        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            token = null;

            if (inputBytes.Peek() != '<' || !inputBytes.MoveNext())
            {
                return false;
            }


#if NET8_0_OR_GREATER
            var peek = inputBytes.PeekBuffer().Span;

            int n = peek.IndexOfAnyExcept("0123456789abcdefABCDEF"u8);

            if (n >= 0 && peek[n] == '>')
            {
                // Fast path - no whitespace or invalid characters.
                var hexSpan = peek.Slice(0, n);
                token = new HexToken(hexSpan);
                inputBytes.Seek(inputBytes.CurrentOffset + n + 1);
                return true;
            }
#endif

            using var charBuffer = new ArrayPoolBufferWriter<char>();

            while (inputBytes.MoveNext())
            {
                var current = inputBytes.CurrentByte;

                if (ReadHelper.IsWhitespace(current))
                {
                    continue;
                }

                if (current == '>')
                {
                    break;
                }

                if (!IsValidHexCharacter(current))
                {
                    return false;
                }

                charBuffer.Write((char)current);
            }

            token = new HexToken(charBuffer.WrittenSpan);

            return true;
        }

        private static bool IsValidHexCharacter(byte b)
        {
            return (b >= '0' && b <= '9')
                   || (b >= 'a' && b <= 'f')
                   || (b >= 'A' && b <= 'F');
        }
    }
}