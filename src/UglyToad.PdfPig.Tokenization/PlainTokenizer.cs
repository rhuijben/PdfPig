namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using Tokens;

    internal sealed class PlainTokenizer : InputByteTokenizer
    {
        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            var firstByte = inputBytes.Peek() ?? 0;
            if (firstByte == 0 || ReadHelper.IsWhitespace(firstByte))
            {
                token = null;

                return false;
            }

            using var builder = new ValueStringBuilder(stackalloc char[16]);

            inputBytes.MoveNext();
            builder.Append((char)inputBytes.CurrentByte);

            while (inputBytes.Peek() is { } b
                && !ReadHelper.IsWhitespace(b)
                && (char)b is not '<' and not '[' and not '/' and not ']' and not '>' and not '(' and not ')')
            {
                inputBytes.MoveNext();
                builder.Append((char) inputBytes.CurrentByte);
            }

            var text = builder.AsSpan();

            token = text switch {
                "true"  => BooleanToken.True,
                "false" => BooleanToken.False,
                "null"  => NullToken.Instance,
                _       => OperatorToken.Create(text),
            };

            return true;
        }
    }
}
