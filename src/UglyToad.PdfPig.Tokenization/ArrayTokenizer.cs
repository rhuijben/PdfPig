namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using Scanner;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Tokens;

    internal sealed class ArrayTokenizer : InputByteTokenizer
    {
        private readonly bool usePdfDocEncoding;

        public ArrayTokenizer(bool usePdfDocEncoding)
        {
            this.usePdfDocEncoding = usePdfDocEncoding;
        }

        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            if (inputBytes.Peek() != '[' || !inputBytes.MoveNext())
            {
                token = null;
                return false;
            }

            var scanner = new CoreTokenScanner(inputBytes, usePdfDocEncoding, ScannerScope.Array);

            var contents = new List<IToken>();

            IToken? previousToken = null;
            while (scanner.MoveNext())
            {
                previousToken = scanner.CurrentToken;

                if (scanner.CurrentToken is CommentToken)
                {
                    continue;
                }
                
                contents.Add(scanner.CurrentToken!);
            }

            if (inputBytes.Peek() == ']')
            {
                inputBytes.MoveNext(); // Read that ']' character.
                token = new ArrayToken(contents);

                return true;
            }
            else
            {
                token = null;
                return false;
            }
        }
    }
}
