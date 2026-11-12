using System.Buffers;
using System.Buffers.Binary;

namespace DeviceLink.Protocol
{
    /// <summary>Drone telemetry: coordinates in degrees × 10^7 (integers, as in MAVLink).</summary>
    public readonly record struct Telemetry(int LatE7, int LonE7)
    {
        /// <summary>Frame type code for telemetry.</summary>
        public const byte MessageType = 0x01;
        /// <summary>Payload size in bytes: two big-endian int32 values.</summary>
        public const int Size = 8;
        private const double Scale = 1e7;

        /// <summary>Latitude in degrees.</summary>
        public double Latitude => LatE7 / Scale;    
        /// <summary>Longitude in degrees.</summary>
        public double Longitude => LonE7 / Scale;    

        /// <summary>Creates telemetry from coordinates in degrees, rounded to 1e-7.</summary>
        public static Telemetry FromDegress(double lat, double lon)
        {
            return new Telemetry(
                LatE7: (int)Math.Round(lat * Scale),
                LonE7: (int)Math.Round(lon * Scale));
        }

        /// <summary>Serializes the coordinates into 8 big-endian bytes.</summary>
        public void WriteTo(Span<byte> destination)
        {
            BinaryPrimitives.WriteInt32BigEndian(destination, LatE7);
            BinaryPrimitives.WriteInt32BigEndian(destination[4..], LonE7);
        }

        /// <summary>Deserializes telemetry from a frame payload; false if the size does not match.</summary>
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

        /// <summary>Writes the telemetry as a complete protocol frame with the given sequence number.</summary>
        public void WriteFrame(IBufferWriter<byte> writer, byte sequence)
        {
            Span<byte> payload = stackalloc byte[Size];
            WriteTo(payload);
            FrameCodec.Write(writer, MessageType, sequence, payload);
        }
    }
}