using System.Buffers;
using System.Buffers.Binary;

namespace DeviceLink.Protocol
{
    public readonly record struct Telemetry(int LatE7, int LonE7)
    {
        public const byte MessageType = 0x01;
        public const int Size = 8;
        private const double Scale = 1e7;

        public double Latitude => LatE7 / Scale;    
        public double Longitude => LonE7 / Scale;    

        public static Telemetry FromDegress(double lat, double lon)
        {
            return new Telemetry(
                LatE7: (int)Math.Round(lat * Scale),
                LonE7: (int)Math.Round(lon * Scale));
        }

        public void WriteTo(Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32BigEndian(destination, LatE7);
            BinaryPrimitives.WriteInt32BigEndian(destination[4..], LonE7);
        }

        public static bool TryRead(ReadOnlySequence<byte> payload, out Telemetry telemetry)
        {
            telemetry = default;
            if (payload.Length != Size) return false;

            Span<byte> buffer = stackalloc byte[Size];
            payload.CopyTo(buffer);

            telemetry = new Telemetry(
                BinaryPrimitives.ReadInt32BigEndian(buffer),
                BinaryPrimitives.ReadInt32BigEndian(buffer[4..])
                );

            return true;
        }

        public void WriteFrame(IBufferWriter<byte> writer, byte sequence)
        {
            Span<byte> payload = stackalloc byte[Size];
            WriteTo(payload);
            FrameCodec.Write(writer, MessageType, sequence, payload);
        }
    }
}