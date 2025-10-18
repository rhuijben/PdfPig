namespace UglyToad.PdfPig.Core
{
    using System;
    using System.Diagnostics;

    /// <inheritdoc />
    /// <summary>
    /// Input bytes from a byte array.
    /// </summary>
    public sealed class MemoryInputBytes : IInputBytes
    {
        private readonly ReadOnlyMemory<byte> memory;

        /// <summary>
        /// Create a new <see cref="MemoryInputBytes"/>.
        /// </summary>
        [DebuggerStepThrough]
        public MemoryInputBytes(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
            this.currentOffset = 0;
        }

        private int currentOffset;
        /// <inheritdoc />
        public long CurrentOffset => currentOffset;

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (currentOffset >= memory.Length)
            {
                return false;
            }

            CurrentByte = memory.Span[currentOffset++];
            return true;
        }

        /// <inheritdoc />
        public byte CurrentByte { get; private set; }

        /// <inheritdoc />
        public long Length => memory.Length;

        /// <inheritdoc />
        public byte? Peek()
        {
            int readOffset = currentOffset;

            return (readOffset >= 0 && readOffset < memory.Length) ? memory.Span[readOffset] : null;
        }

        /// <inheritdoc />
        public bool IsAtEnd()
        {
            return currentOffset >= memory.Length;
        }

        /// <inheritdoc />
        public void Seek(long position)
        {
            if (position < 0 || position > memory.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, message: null);
            }

            currentOffset = (int)position;
            int readOffset = currentOffset - 1;
            CurrentByte = (readOffset >= 0 && readOffset <= memory.Length) ? memory.Span[readOffset] : (byte)0;
        }

        /// <inheritdoc />
        public int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            var viableLength = (memory.Length - currentOffset);
            var readLength = viableLength < buffer.Length ? viableLength : buffer.Length;
            var startFrom = currentOffset;

            memory.Span.Slice(startFrom, readLength).CopyTo(buffer);

            if (readLength > 0)
            {
                currentOffset += readLength;
                CurrentByte = buffer[readLength - 1];
            }

            return readLength;
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> PeekBuffer()
        {
            int startFrom = currentOffset;
            int length = memory.Length - startFrom;

            if (length <= 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return memory.Slice(startFrom, length);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // No resources to dispose
        }
    }
}