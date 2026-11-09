using System.Buffers;
using System.Buffers.Binary;

namespace DeviceLink.Protocol
{
    public static class FrameCodec
    {
        public const int HeaderSize = 6;
        public const int CrcSize = 2;
        public const int MaxPayload = 1024;
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

        private enum ParseStatus { Success, NeedData, Invalid}

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