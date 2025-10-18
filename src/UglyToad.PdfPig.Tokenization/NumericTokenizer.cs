#nullable enable
namespace UglyToad.PdfPig.Tokenization
{
    using Core;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using Tokens;

    internal sealed class NumericTokenizer : InputByteTokenizer
    {

        public override bool TryTokenize(IInputBytes inputBytes, [NotNullWhen(true)] out IToken? token)
        {
            token = null;

            int peekOffset = 0;
            int readBytes = 0;
            int length = 0;

            // Everything before the decimal part.
            var isNegative = false;
            long integerPart = 0;

            // Everything after the decimal point.
            var hasFraction = false;
            long fractionalPart = 0;
            var fractionalCount = 0;

            // Support scientific notation in some font files.
            var hasExponent = false;
            var isExponentNegative = false;
            var exponentPart = 0;

            var acceptSign = true;
            ReadOnlyMemory<byte> buffer = inputBytes.PeekBuffer();
            ReadOnlySpan<byte> span = buffer.Span;

            // Process currentByte first, then bytes from buffer
            while (true)
            {
                if (peekOffset > 0 && peekOffset >= buffer.Length)
                {
                    // End of buffer and we are not done yet. Try reading further
                    inputBytes.Seek(inputBytes.CurrentOffset + peekOffset);
                    readBytes += peekOffset;
                    peekOffset = 0;
                    buffer = inputBytes.PeekBuffer(); // Guarantees new data

                    if (buffer.Length == 0)
                    {
                        // No more data to read. EOF
                        break;
                    }
                    span = buffer.Span;
                }

                byte b = span[peekOffset];

                // Process current byte
                if (b >= '0' && b <= '9')
                {
                    var value = b - '0';
                    if (hasExponent)
                    {
                        exponentPart = (exponentPart * 10) + value;
                    }
                    else if (hasFraction)
                    {
                        fractionalPart = (fractionalPart * 10) + value;
                        fractionalCount++;
                    }
                    else
                    {
                        integerPart = (integerPart * 10) + value;
                    }
                    acceptSign = false;
                }
                else if (b == '+' && acceptSign)
                {
                    acceptSign = false;
                }
                else if (b == '-' && acceptSign)
                {
                    if (hasExponent)
                    {
                        isExponentNegative = true;
                    }
                    else
                    {
                        isNegative = true;
                    }
                }
                else if (b == '.' && !hasExponent && !hasFraction)
                {
                    hasFraction = true;
                    acceptSign = false;
                }
                else if ((b == 'e' || b == 'E') && length > 0 && !hasExponent)
                {
                    hasExponent = true;
                    acceptSign = true;
                }
                else
                {
                    // No valid first character or end of number
                    if (length == 0)
                    {
                        if (readBytes > 0)
                        {
                            // Very unlikely. But restore state
                            inputBytes.Seek(inputBytes.CurrentOffset - readBytes);
                        }
                        return false;
                    }
                    break;
                }

                peekOffset++;
                length++;
            }

            // Fix position by reading the bytes we used
            inputBytes.Seek(inputBytes.CurrentOffset + peekOffset);

            token = ComputeNumericToken(integerPart, fractionalPart, fractionalCount,
                exponentPart, isNegative, hasFraction, hasExponent, isExponentNegative);
            return true;
        }


        private static IToken ComputeNumericToken(long longPart, long fractionalPart, int fractionalCount,
            int exponentPart, bool isNegative, bool hasFraction, bool hasExponent, bool isExponentNegative)
        {
            if (!hasExponent && !hasFraction)
            {
                return isNegative ? NumericToken.Create(-longPart) : NumericToken.Create(longPart);
            }

            double integerPart = longPart;
            if (hasExponent && !isExponentNegative)
            {
                // Apply the multiplication before any fraction logic to avoid loss of precision.
                // E.g. 1.53E3 should be exactly 1,530.

                // TODO: Maybe optimize directly towards IntegerToken if safe

                // Move the whole part to the left of the decimal point.
                var combined = integerPart * Pow10(fractionalCount) + fractionalPart;

                // For 1.53E3 we changed this to 153 above, 2 fractional parts, so now we are missing (3-2) 1 additional power of 10.
                var shift = exponentPart - fractionalCount;

                if (shift >= 0)
                {
                    integerPart = combined * Pow10(shift);
                }
                else
                {
                    // Still a positive exponent, but not enough to fully shift
                    // For example 1.457E2 becomes 1,457 but shift is (2-3) -1, the outcome should be 145.7
                    integerPart = combined / Pow10(-shift);
                }

                hasFraction = false;
                hasExponent = false;
            }

            if (hasFraction && fractionalCount > 0)
            {
                // Use optimized division with multiplication for common cases
                integerPart += fractionalCount switch
                {
                    1 => fractionalPart / 10.0,
                    2 => fractionalPart / 100.0,
                    3 => fractionalPart / 1000.0,
                    //4 => fractionalPart / 10000.0,
                    //5 => fractionalPart / 100000.0,
                    //6 => fractionalPart / 1000000.0,
                    _ => fractionalPart / Math.Pow(10, fractionalCount)
                };
            }

            if (hasExponent)
            {
                var signedExponent = isExponentNegative ? -exponentPart : exponentPart;
                integerPart *= Math.Pow(10, signedExponent);
            }

            if (isNegative)
            {
                integerPart = -integerPart;
            }

            return integerPart == 0 ? NumericToken.Zero : new NumericToken(integerPart);
        }

        private static double Pow10(int exp)
        {
            return exp switch
            {
                0 => 1,
                1 => 10,
                2 => 100,
                3 => 1000,
                4 => 10000,
                5 => 100000,
                6 => 1000000,
                7 => 10000000,
                8 => 100000000,
                9 => 1000000000,
                10 => 10000000000,
                11 => 100000000000,
                12 => 1000000000000,
                _ => Math.Pow(10, exp)
            };
        }
    }
}