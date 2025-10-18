namespace UglyToad.PdfPig.Tokenization.Scanner
{
    using System.Diagnostics.CodeAnalysis;
    using Tokens;

    /// <summary>
    /// Scan input for PostScript/PDF tokens.
    /// </summary>
    public interface ITokenScanner
    {
        /// <summary>
        /// Read the next token in the input.
        /// </summary>
        /// <returns></returns>
        bool MoveNext();

        /// <summary>
        /// The currently read token.
        /// </summary>
        IToken? CurrentToken { get; }

        /// <summary>
        /// Try reading a token of the specific type.
        /// </summary>
        bool TryReadToken<T>([NotNullWhen(true)] out T? token) where T : class, IToken;
    }
}