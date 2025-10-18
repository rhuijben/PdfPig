namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using Tokens;

    internal sealed class CommentTokenizer : InputByteTokenizer
    {
        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            token = null;

            if (inputBytes.Peek() != '%' || !inputBytes.MoveNext())
            {
                return false;
            }

            using var builder = new ValueStringBuilder(stackalloc char[32]);

            while (inputBytes.Peek() is { } c && !ReadHelper.IsEndOfLine(c))
            {
                inputBytes.MoveNext();
                builder.Append((char) inputBytes.CurrentByte);
            }

            token = new CommentToken(builder.ToString());

            return true;
        }
    }
}
