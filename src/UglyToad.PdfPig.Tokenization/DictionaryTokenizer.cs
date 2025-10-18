namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using Scanner;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Tokens;

    internal sealed class DictionaryTokenizer : InputByteTokenizer
    {
        private readonly bool usePdfDocEncoding;
        private readonly IReadOnlyList<NameToken>? requiredKeys;
        private readonly bool useLenientParsing;

        /// <summary>
        /// Create a new <see cref="DictionaryTokenizer"/>.
        /// </summary>
        /// <param name="usePdfDocEncoding">
        /// Whether to read strings using the PdfDocEncoding.
        /// </param>
        /// <param name="requiredKeys">
        /// Can be provided to recover from errors with missing dictionary end symbols if the
        /// set of keys expected in the dictionary are known.
        /// </param>
        /// <param name="useLenientParsing">Whether to use lenient parsing.</param>
        public DictionaryTokenizer(bool usePdfDocEncoding, IReadOnlyList<NameToken>? requiredKeys = null, bool useLenientParsing = false)
        {
            this.usePdfDocEncoding = usePdfDocEncoding;
            this.requiredKeys = requiredKeys;
            this.useLenientParsing = useLenientParsing;
        }

        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            if (inputBytes.Peek() != '<' || !inputBytes.MoveNext())
            {
                token = null;
                return false;
            }

            var start = inputBytes.CurrentOffset;

            try
            {
                return TryTokenizeInternal((byte)'<', inputBytes, false, out token);
            }
            catch (PdfDocumentFormatException)
            {
                // Cannot attempt inferred end.
                if (requiredKeys == null)
                {
                    throw;
                }
            }

            inputBytes.Seek(start);

            return TryTokenizeInternal((byte)'<', inputBytes, true, out token);
        }

        private bool TryTokenizeInternal(byte currentByte, IInputBytes inputBytes, bool useRequiredKeys, [NotNullWhen(true)] out IToken? token)
        {
            token = null;

            bool foundNextOpenBrace = false;

            while (inputBytes.MoveNext())
            {
                if (inputBytes.CurrentByte == '<')
                {
                    foundNextOpenBrace = true;
                    break;
                }

                if (!ReadHelper.IsWhitespace(inputBytes.CurrentByte))
                {
                    break;
                }
            }

            if (!foundNextOpenBrace)
            {
                return false;
            }

            var coreScanner = new CoreTokenScanner(inputBytes, usePdfDocEncoding, ScannerScope.Dictionary, useLenientParsing: useLenientParsing);

            var tokens = new List<IToken>();

            while (coreScanner.MoveNext())
            {
                if (coreScanner.CurrentToken is CommentToken)
                {
                    continue;
                }

                tokens.Add(coreScanner.CurrentToken!);

                // Has enough key/values for each required key
                if (useRequiredKeys && tokens.Count >= requiredKeys!.Count * 2)
                {
                    var proposedDictionary = ConvertToDictionary(tokens, useLenientParsing);

                    var isAcceptable = true;
                    foreach (var key in requiredKeys)
                    {
                        if (!proposedDictionary.TryGetValue(key, out var tok) || tok == null)
                        {
                            isAcceptable = false;
                            break;
                        }
                    }

                    // If each required key has a value and we're here because parsing broke previously then return
                    // this dictionary.
                    if (isAcceptable)
                    {
                        token = new DictionaryToken(proposedDictionary);
                        return true;
                    }
                }
            }

            if (inputBytes.Peek() == '>')
            {
                // Clean dictionary close
                inputBytes.MoveNext();

                if (inputBytes.Peek() == '>')
                {
                    inputBytes.MoveNext(); // Read the final '>' of a '<<' dictionary close. Should always be there
                }

                var dictionary = ConvertToDictionary(tokens, useLenientParsing);

                token = new DictionaryToken(dictionary);

                return true;
            }
            else if (/* Allow missing dictionary close && */ tokens.Count > 0) // Required for CanParseCidSystemInfoAndOtherInformationWhenMissingDictionaryClose test
            {
                // Missing close token but let's return what we have.
                var dictionary = ConvertToDictionary(tokens, useLenientParsing);

                token = new DictionaryToken(dictionary);

                return true;
            }
            else
            {
                token = null;
                return false;
            }
        }

        private static Dictionary<NameToken, IToken> ConvertToDictionary(List<IToken> tokens, bool useLenientParsing)
        {
            var result = new Dictionary<NameToken, IToken>();

            NameToken? key = null;
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (key == null)
                {
                    if (token is NameToken name)
                    {
                        key = name;
                        continue;
                    }

                    if (useLenientParsing)
                    {
                        // TODO - Log warning
                        System.Diagnostics.Debug.WriteLine($"Expected name as dictionary key, instead got: " + token);
                        continue;
                    }

                    throw new PdfDocumentFormatException($"Expected name as dictionary key, instead got: " + token);
                }

                // Combine indirect references, e.g. 12 0 R
                if (token is NumericToken num && PeekNext(tokens, i) is NumericToken gen)
                {
                    var r = PeekNext(tokens, i + 1);

                    if (r == OperatorToken.R)
                    {
                        result[key] = new IndirectReferenceToken(new IndirectReference(num.Long, gen.Int));
                        i = i + 2;
                    }
                }
                else
                {
                    result[key] = token;
                }

                // skip def.
                if (PeekNext(tokens, i) == OperatorToken.Def)
                {
                    i++;
                }

                key = null;
            }

            return result;
        }

        private static IToken? PeekNext(List<IToken> tokens, int currentIndex)
        {
            if (tokens.Count - 1 < currentIndex + 1)
            {
                return null;
            }

            return tokens[currentIndex + 1];
        }
    }
}
