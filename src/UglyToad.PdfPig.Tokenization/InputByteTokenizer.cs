namespace UglyToad.PdfPig.Tokenization
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.Tokens;

    /// <summary>
    /// Baseclass for a optimized tokenizers that don't read ahead
    /// </summary>
    public abstract class InputByteTokenizer : ITokenizer
    {
        bool ITokenizer.ReadsNextByte => false;

        bool ITokenizer.TryTokenize(byte currentByte, IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            var p = inputBytes.CurrentOffset;
            inputBytes.Seek(p - 1);

            if (TryTokenize(inputBytes, out token))
            {
                return true;
            }
            else
            {
                inputBytes.Seek(p);
                token = null;
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputBytes"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public abstract bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token);


        /// <summary>
        /// Creates a ByteTokenizer from an existing tokenizer.
        /// </summary>
        /// <param name="tokenizer"></param>
        /// <returns></returns>
        public static InputByteTokenizer CreateFrom(ITokenizer tokenizer)
        {
            if (tokenizer is null)
            {
                throw new ArgumentNullException(nameof(tokenizer));
            }

            if (tokenizer is InputByteTokenizer byteTokenizer)
            {
                return byteTokenizer;
            }
            else if (tokenizer.ReadsNextByte)
            {
                return new WrapperReadsNextByte(tokenizer);
            }
            else
            {
                return new Wrapper(tokenizer);
            }
        }

        private sealed class Wrapper : InputByteTokenizer
        {
            private readonly ITokenizer tokenizer;
            public Wrapper(ITokenizer tokenizer)
            {
                this.tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            }

            public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
            {
                long pos = inputBytes.CurrentOffset;

                if (inputBytes.MoveNext())
                {
                    bool ok = tokenizer.TryTokenize(inputBytes.CurrentByte, inputBytes, out token);

                    if (!ok)
                    {
                        inputBytes.Seek(pos);
                    }

                    return ok;
                }
                token = null;
                return false;
            }
        }

        private sealed class WrapperReadsNextByte : InputByteTokenizer
        {
            private readonly ITokenizer tokenizer;

            public WrapperReadsNextByte(ITokenizer tokenizer)
            {
                this.tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            }
            public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
            {
                long pos = inputBytes.CurrentOffset;

                if (inputBytes.MoveNext())
                {
                    bool ok = tokenizer.TryTokenize(inputBytes.CurrentByte, inputBytes, out token);

                    if (!ok)
                    {
                        inputBytes.Seek(pos);
                    }
                    else
                    {
                        inputBytes.Seek(inputBytes.CurrentOffset - 1);
                    }
                    return ok;
                }
                token = null;
                return false;
            }
        }
    }
}
