namespace UglyToad.PdfPig.Core
{
    using System;

    /// <inheritdoc />
    /// <summary>
    /// The input bytes for a PDF document.
    /// </summary>
    public interface IInputBytes : IDisposable
    {
        /// <summary>
        /// The current offset in bytes.
        /// </summary>
        long CurrentOffset { get; }

        /// <summary>
        /// Moves to the next byte if available.
        /// </summary>
        bool MoveNext();

        /// <summary>
        /// The current byte.
        /// </summary>
        byte CurrentByte { get; }

        /// <summary>
        /// The length of the data in bytes.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Returns the next byte if available.
        /// </summary>
        byte? Peek();
        
        /// <summary>
        /// Whether we are at the end of the available data.
        /// </summary>
        bool IsAtEnd();

        /// <summary>
        /// Move to a given position. Resets <see cref="CurrentByte"/> to the value read when at <paramref name="position"/>,
        /// </summary>
        void Seek(long position);

        /// <summary>
        /// Fill the buffer with bytes starting from the current position.
        /// </summary>
        /// <param name="buffer">A buffer with a length corresponding to the number of bytes to read.</param>
        /// <returns>The number of bytes successfully read.</returns>
        int Read(Span<byte> buffer);

        /// <summary>
        /// Returns a memory block containing all the bytes that have been read ahead but not consumed.
        /// </summary>
        /// <returns></returns>
        ReadOnlyMemory<byte> PeekBuffer();
    }
}