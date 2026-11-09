using DeviceLink.Protocol;
using System.Buffers;

namespace DeviceLink.Tests
{
    public class TelemetryTests
    {
        private static readonly byte[] SpecFrame= 
            [
                0xAA, 0x55,
                0x00, 0x08,
                0x01,
                0x00,
                0x1E,0x12, 0x13, 0x08, 0x12, 0x31, 0x80, 0x50,
                0x2E,0x71
            ];

        private static readonly byte[] SpecPayload = SpecFrame[6..14];

        [Fact]
        public void Telemetry_WriteTo_ProducesSpecPayload()
        {
            var payload = new byte[Telemetry.Size];
            Telemetry.FromDegress(50.4501, 30.5234).WriteTo(payload);
            Assert.Equal(SpecPayload, payload);
        }

        [Fact]
        public void Telemerty_TryRead_ReturnsCoordinate()
        {
            var payload = new ReadOnlySequence<byte>(SpecPayload);
            Assert.True(Telemetry.TryRead(payload, out Telemetry telemetry));
            Assert.Equal(new Telemetry(504_501_000, 305_234_000), telemetry);
        }

        [Fact]
        public void Telemetry_WrongLength_ReturnsFalse()
        {
            Assert.False(Telemetry.TryRead(new ReadOnlySequence<byte>(new byte[7]), out _));
            Assert.False(Telemetry.TryRead(new ReadOnlySequence<byte>(new byte[9]), out _));
        }

        [Fact]
        public void Telemetry_FullFrame_Write_MatchSpec()
        {
            Span<byte> payload = stackalloc byte[Telemetry.Size];
            Telemetry.FromDegress(50.4501, 30.5234).WriteTo(payload);

            var writer = new ArrayBufferWriter<byte>();
            FrameCodec.Write(writer, Telemetry.MessageType, 0, payload);

            Assert.Equal(SpecFrame, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public void Telemetry_FullFrame_ReturnsCoord()
        {
            var buffer = new ReadOnlySequence<byte>(SpecFrame);

            Assert.True(FrameCodec.TryParse(ref buffer, out var frame));
            Assert.Equal(Telemetry.MessageType, frame.Type);
            Assert.Equal((byte)0, frame.Sequence);
            Assert.True(Telemetry.TryRead(frame.Payload, out var telemetry));
            Assert.Equal(new Telemetry(504_501_000, 305_234_000), telemetry);
            Assert.True(buffer.IsEmpty);
        }

        [Fact]
        public void Telemetry_WriteFrame_MatchesSpec()
        {
            var writer = new ArrayBufferWriter<byte>();
            Telemetry.FromDegress(50.4501, 30.5234).WriteFrame(writer, sequence: 0);
            Assert.Equal(SpecFrame, writer.WrittenSpan.ToArray());
        }
    }
}