namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using Tokens;

#if NET
    using System.Text.Unicode;
#endif

    internal sealed class NameTokenizer : InputByteTokenizer
    {
#if NET
        static NameTokenizer()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
#endif

        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            token = null;

            if (inputBytes.Peek() != '/' || !inputBytes.MoveNext())
            {
                return false;
            }

            using var bytes = new ArrayPoolBufferWriter<byte>();

            bool escapeActive = false;
            int postEscapeRead = 0;
            Span<char> escapedChars = stackalloc char[2];

            while (inputBytes.Peek() is { } b)
            {
                if (b == '#')
                {
                    escapeActive = true;
                }
                else if (escapeActive)
                {
                    if (ReadHelper.IsHex((char)b))
                    {
                        escapedChars[postEscapeRead] = (char)b;
                        postEscapeRead++;

                        if (postEscapeRead == 2)
                        {
                            // We validated that the char is hex. So assume ASCII rules apply and shortcut hex decoding
                            bytes.Write(HexToken.ConvertPair(escapedChars[0], escapedChars[1]));

                            escapeActive = false;
                            postEscapeRead = 0;
                        }
                    }
                    else
                    {
                        bytes.Write((byte)'#');

                        if (postEscapeRead == 1)
                        {
                            bytes.Write((byte)escapedChars[0]);
                        }

                        if (ReadHelper.IsEndOfName(b))
                        {
                            break;
                        }

                        if (b == '#')
                        {
                            // Make it clear what's going on, we read something like #m#AE
                            // ReSharper disable once RedundantAssignment
                            escapeActive = true;
                            postEscapeRead = 0;
                            continue;
                        }

                        bytes.Write(b);
                        escapeActive = false;
                        postEscapeRead = 0;
                    }

                }
                else if (ReadHelper.IsEndOfName(b))
                {
                    break;
                }
                else
                {
                    bytes.Write(b);
                }

                inputBytes.MoveNext();
            }

#if NET8_0_OR_GREATER
            var byteArray = bytes.WrittenSpan;
            bool isValidUtf8 = Utf8.IsValid(byteArray);
#else
            var byteArray = bytes.WrittenSpan.ToArray();
            bool isValidUtf8 = ReadHelper.IsValidUtf8(byteArray);
#endif

            var str = isValidUtf8
                ? Encoding.UTF8.GetString(byteArray)
                : Encoding.GetEncoding("windows-1252").GetString(byteArray);

            token = NameToken.Create(str);

            return true;
        }
    }
}