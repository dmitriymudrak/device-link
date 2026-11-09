using DeviceLink.Protocol;
using System.Buffers;

namespace DeviceLink.Tests
{
    public class FrameCodecWriteTests
    {
        [Fact]
        public void Write_TelemetryPayload_ProducesSpecFrame()
        {
            byte[] payload = [0x1E, 0x12, 0x13, 0x08, 0x12, 0x31, 0x80, 0x50];
            
            byte[] expected =
            [
                0xAA, 0x55, 0x00, 0x08, 0x01, 0x00,
                0x1E, 0x12, 0x13, 0x08, 0x12, 0x31, 0x80, 0x50,
                0x2E, 0x71
            ];

            var output = new ArrayBufferWriter<byte>();

            FrameCodec.Write(output, 0x01, 0, payload);

            Assert.Equal(expected, output.WrittenSpan.ToArray());
        }

        [Fact]
        public void Write_PayloadOverLimit_ThrowsException()
        {
            byte[] payload = new byte[FrameCodec.MaxPayload + 1];
            var output = new ArrayBufferWriter<byte>();

            Assert.Throws<ArgumentException>(
                () => FrameCodec.Write(output, 0x01, 0, payload)
            );
        }
    }
}