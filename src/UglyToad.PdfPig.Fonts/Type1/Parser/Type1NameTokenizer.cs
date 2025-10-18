namespace UglyToad.PdfPig.Fonts.Type1.Parser
{
    using System.Text;
    using Core;
    using Tokens;
    using Tokenization;

    /// <inheritdoc />
    public sealed class Type1NameTokenizer : ITokenizer
    {
        /// <inheritdoc />
        public bool ReadsNextByte => false;

        /// <inheritdoc />
        public bool TryTokenize(byte currentByte, IInputBytes inputBytes, out IToken token)
        {
            token = null;

            if (currentByte != '/')
            {
                return false;
            }

            var builder = new StringBuilder();
            while (inputBytes.Peek() is { } b
                && !ReadHelper.IsWhitespace(b) 
                && (char)b is not '{' and not '<' and not '/' and not '[' and not '(')
            {
                inputBytes.MoveNext();
                builder.Append((char)inputBytes.CurrentByte);
            }

            token = NameToken.Create(builder.ToString());

            return true;
        }
    }
}
