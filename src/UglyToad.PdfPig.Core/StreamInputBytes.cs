namespace UglyToad.PdfPig.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;

    /// <inheritdoc />
    /// <summary>
    /// Input bytes from a stream with buffering for efficient seeking and peeking.
    /// </summary>
    public sealed class StreamInputBytes : IInputBytes
    {
        private const int BufferSize = 80 * 1024; // 80 KB
        private const int MinForwardBuffer = 4 * 1024; // 4 KB minimum forward data for PeekBuffer
        private const int BackwardKeep = 16 * 1024; // 16 KB to keep for backward seeks

        private readonly Stream stream;
        private readonly bool shouldDispose;
        private readonly byte[] buffer;
        private long? streamLength;

        private long bufferStartPosition; // Position in stream where buffer starts
        private int bufferLength; // Valid data in buffer
        private int currentOffset; // Current position within buffer

        /// <inheritdoc />
        public long CurrentOffset => bufferStartPosition + currentOffset;

        /// <inheritdoc />
        public byte CurrentByte { get; private set; }

        /// <inheritdoc />
        public long Length => streamLength ??= stream.Length;

        /// <summary>
        /// Create a new <see cref="StreamInputBytes"/>.
        /// </summary>
        /// <param name="stream">The stream to use, should be readable and seekable.</param>
        /// <param name="shouldDispose">Whether this class should dispose the stream once finished.</param>
        public StreamInputBytes(Stream stream, bool shouldDispose = true)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException("The provided stream did not support reading.", nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new ArgumentException("The provided stream did not support seeking.", nameof(stream));
            }

            this.stream = stream;
            this.shouldDispose = shouldDispose;
            this.streamLength = stream.Length;
            this.buffer = new byte[BufferSize];
            //this.bufferStartPosition = 0;
            //this.bufferLength = 0;
            //this.currentOffset = 0;
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (CurrentOffset >= Length)
            {
                return false;
            }

            if (currentOffset >= bufferLength)
            {
                RefillBuffer(CurrentOffset);
            }

            if (currentOffset < bufferLength)
            {
                // Handle seek to 0. Nothing read yet
                CurrentByte = currentOffset >= 0 ? buffer[currentOffset] : (byte)0;
                currentOffset++;
                return true;
            }
            else
            {
                CurrentByte = 0;
            }

            return false;
        }

        /// <inheritdoc />
        public byte? Peek()
        {
            // Fast path: peek byte is in buffer
            if (currentOffset < bufferLength)
            {
                return buffer[currentOffset];
            }

            // At end of stream?
            if (CurrentOffset >= Length)
            {
                return null;
            }

            // Need more data - refill and try again
            RefillBuffer(CurrentOffset);

            return currentOffset < bufferLength ? buffer[currentOffset] : (byte?)null;
        }

        /// <inheritdoc />
        public bool IsAtEnd()
        {
            return CurrentOffset >= streamLength;
        }

        /// <inheritdoc />
        public void Seek(long position)
        {
            if (position < 0 || position > streamLength)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, message: null);
            }

            position -= 1;
            long targetInBuffer = position - bufferStartPosition;

            // Fast path: seeking within current buffer. -1 because we read the next byte
            if (targetInBuffer >= -1 && targetInBuffer < bufferLength)
            {
                currentOffset = (int)targetInBuffer;
            }
            else
            {
                // Far seek: reload buffer centered on target
                RefillBuffer(position);
            }
            MoveNext(); // Prepare the next byte in CurrentByte
        }

        /// <inheritdoc />
        public int Read(Span<byte> destination)
        {
            if (destination.IsEmpty)
            {
                return 0;
            }

            int requestedLength = destination.Length;

            if (requestedLength > streamLength - CurrentOffset)
            {
                // Handle EOF
                requestedLength = (int)(Length - CurrentOffset);

                if (requestedLength == 0)
                {
                    return 0;
                }
            }

            if (requestedLength <= bufferLength - currentOffset)
            {
                // Can read from buffer
                buffer.AsSpan(currentOffset, requestedLength).CopyTo(destination);
                currentOffset += requestedLength;
                CurrentByte = buffer[currentOffset-1];
                return requestedLength;
            }

            // Not in buffer. Large read... lets do this the old fashioned way
            stream.Seek(CurrentOffset, SeekOrigin.Begin);
            Span<byte> writeTo = destination.Length > buffer.Length ? destination : buffer;
            int totalRead = stream.Read(writeTo);
            int produced;

            if (writeTo != destination)
            {
                // Used our buffer to read more
                produced = Math.Min(totalRead, destination.Length);
                writeTo.Slice(0, produced).CopyTo(destination);
                bufferStartPosition = CurrentOffset;
                currentOffset = produced;
                bufferLength = totalRead;
            }
            else
            {
                bufferLength = 0; // Invalidate buffer
                bufferStartPosition = CurrentOffset + totalRead;
                currentOffset = 0;
                // Buffer will refill on next read, peek, etc.
                produced = totalRead;
            }

            CurrentByte = produced > 0 ? writeTo[produced - 1] : (byte)0;

            return produced;
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> PeekBuffer()
        {
            int available = bufferLength - currentOffset;

            // If we have less than MinForwardBuffer available and there's more data in stream, refill
            if (available < MinForwardBuffer)
            {
                long remainingInStream = Length - CurrentOffset;
                if (remainingInStream > available)
                {
                    RefillBuffer(CurrentOffset);
                    available = bufferLength - currentOffset;
                }
            }

            if (currentOffset >= bufferLength || available <= 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return new ReadOnlyMemory<byte>(buffer, currentOffset, available);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (shouldDispose)
            {
                stream?.Dispose();
            }
        }

        private void RefillBuffer(long seekPosition)
        {
            // Ok, lets assume we have a modern OS with an efficient disk cache and just read the buffer.
            // This keeps the code easy at a very low cose

            long newStartPos = seekPosition & ~0xFFFL; // Align back to 4096 bytes

            newStartPos = Math.Max(0, newStartPos - BackwardKeep); // Try to keep some backward data

            stream.Seek(newStartPos, SeekOrigin.Begin);
            bufferLength = stream.Read(buffer, 0, buffer.Length);

            bufferStartPosition = newStartPos;
            currentOffset = (int)(seekPosition - newStartPos);
        }
    }
}
