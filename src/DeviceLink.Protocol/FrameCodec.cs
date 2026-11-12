using System.Buffers;
using System.Buffers.Binary;

namespace DeviceLink.Protocol
{
    /// <summary>Frame codec: AA 55 | length (u16 BE) | type | seq | payload | CRC-16 (BE) over bytes 2..end of payload.</summary>
    public static class FrameCodec
    {
        /// <summary>Header size: marker (2) + length (2) + type (1) + seq (1).</summary>
        public const int HeaderSize = 6;
        /// <summary>Size of the CRC-16 at the end of the frame.</summary>
        public const int CrcSize = 2;
        /// <summary>Maximum payload length in bytes.</summary>
        public const int MaxPayload = 1024;
        /// <summary>Writes a complete frame (header, payload, CRC) to the output buffer.</summary>
        public static void Write(IBufferWriter<byte> output, byte type, byte sequence,
            ReadOnlySpan<byte> payload)
        {
            if (payload.Length > MaxPayload)
            {
                throw new ArgumentException("Payload is too long", nameof(payload));
            }

            int crcOffset = HeaderSize + payload.Length;
            int frameSize = crcOffset + CrcSize;

            Span<byte> frame = output.GetSpan(frameSize);
            frame[0] = 0xAA;
            frame[1] = 0x55;
            BinaryPrimitives.WriteUInt16BigEndian(frame.Slice(2), (ushort)payload.Length);
            frame[4] = type;
            frame[5] = sequence;
            payload.CopyTo(frame.Slice(HeaderSize));

            var computeBody = frame.Slice(2, crcOffset - 2);

            ushort crc = Crc16.Compute(computeBody);
            BinaryPrimitives.WriteUInt16BigEndian(frame.Slice(crcOffset), crc);

            output.Advance(frameSize);
        }

        /// <summary>Finds and parses the next valid frame, skipping garbage; the buffer is advanced past consumed data.</summary>
        public static bool TryParse(ref ReadOnlySequence<byte> buffer, out Frame frame)
        {
            frame = default;
            var reader = new SequenceReader<byte>(buffer);

            while (TryFindMarker(ref reader))
            {
                ReadOnlySequence<byte> candidate = buffer.Slice(reader.Position);
                ParseStatus status = TryReadFrame(candidate, out frame, out int frameSize);
                if (status == ParseStatus.Success)
                {
                    buffer = candidate.Slice(frameSize);
                    return true;
                }

                if (status == ParseStatus.NeedData)
                {
                    buffer = candidate;
                    return false;
                }

                reader.Advance(1);
            }

            buffer = buffer.Slice(reader.Position);
            return false;
        }

        /// <summary>Advances the reader to the AA 55 marker (or a trailing AA); false if no marker is found.</summary>
        private static bool TryFindMarker(ref SequenceReader<byte> reader)
        {
            while(reader.TryAdvanceTo(0xAA, advancePastDelimiter: false))
            {
                bool hasNextByte = reader.TryPeek(1, out byte next);
                if(!hasNextByte || next == 0x55)
                    return true;

                reader.Advance(1);
            }

            reader.AdvanceToEnd();
            return false;
        }

        /// <summary>Result of a frame read attempt: success, need more data, or corrupt frame.</summary>
        private enum ParseStatus { Success, NeedData, Invalid}

        /// <summary>Reads a frame starting at the marker, validating length and CRC.</summary>
        private static ParseStatus TryReadFrame(
            ReadOnlySequence<byte> candidate, out Frame frame, out int frameSize)
        {
            frame = default;
            frameSize = 0;

            if(candidate.Length < HeaderSize) return ParseStatus.NeedData;

            Span<byte> header = stackalloc byte[HeaderSize];
            candidate.Slice(0, HeaderSize).CopyTo(header);

            int payloadLength = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(2));
            if (payloadLength > MaxPayload) return ParseStatus.Invalid;

            frameSize = HeaderSize + payloadLength + CrcSize;
            if(candidate.Length<frameSize) return ParseStatus.NeedData;

            Span<byte> bytes = stackalloc byte[frameSize];
            candidate.Slice(0,frameSize).CopyTo(bytes);

            int crcOffset = frameSize - CrcSize;
            ushort actual = Crc16.Compute(bytes.Slice(2, crcOffset - 2));
            ushort expected = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(crcOffset));
            if(actual != expected) 
                return ParseStatus.Invalid;

            frame = new Frame(header[4], header[5], candidate.Slice(HeaderSize, payloadLength));

            return ParseStatus.Success;
        }
    }
}