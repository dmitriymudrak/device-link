using DeviceLink.Protocol;
using System.Buffers;

namespace DeviceLink.Tests
{
    public class FrameCodecParseTests
    {
        private static readonly byte[] SpecPayload = [0x1E, 0x12, 0x13, 0x08, 0x12, 0x31, 0x80, 0x50];
        private static readonly byte[] SpecFrame = [
                0xAA, 0x55, 0x00, 0x08, 0x01, 0x00,
                0x1E, 0x12, 0x13, 0x08, 0x12, 0x31, 0x80, 0x50,
                0x2E, 0x71
            ];

        [Fact]
        public void TryParse_WholeFrame_ReturnsFrameAndConsumesBuffer()
        {
            var buffer = new ReadOnlySequence<byte>(SpecFrame);

            bool parsed = FrameCodec.TryParse(ref buffer, out Frame frame);

            Assert.True(parsed);
            Assert.Equal(0x01, frame.Type);
            Assert.Equal(0, frame.Sequence);
            Assert.Equal(SpecPayload, frame.Payload.ToArray());
            Assert.True(buffer.IsEmpty);
        }

        [Fact]
        public void TryParse_TwoFramesInBuffer_ReturnsBothInOrder()
        {
            var writer = new ArrayBufferWriter<byte>();
            FrameCodec.Write(writer, type: 0x01, sequence:0, SpecPayload);
            FrameCodec.Write(writer, type: 0x01, sequence:1, SpecPayload);
            var buffer = new ReadOnlySequence<byte>(writer.WrittenMemory);

            Assert.True(FrameCodec.TryParse(ref buffer, out Frame first));
            Assert.True(FrameCodec.TryParse(ref buffer, out Frame second));
            Assert.False(FrameCodec.TryParse(ref buffer, out _));

            Assert.Equal(0, first.Sequence);
            Assert.Equal(1, second.Sequence);

            Assert.True(buffer.IsEmpty);
        }

        [Fact]
        public void TryParse_ByteByByte_ReturnsFrameOnlyWhenComplete()
        {
            for(var length=1; length < SpecFrame.Length;length++)
            {
                var partial = new ReadOnlySequence<byte>(SpecFrame, 0, length);

                Assert.False(FrameCodec.TryParse(ref partial, out Frame first));
                Assert.Equal(length, partial.Length);
            }

            var whole = new ReadOnlySequence<byte>(SpecFrame);
            Assert.True(FrameCodec.TryParse(ref whole, out Frame frame));
            Assert.Equal(SpecPayload, frame.Payload.ToArray());
        }

        [Fact]
        public void TryParse_GarbageBeforeFrame_SkipsGarbage()
        {
            byte[] garbage = [0x01, 0x03, 0xAA, 0x55, 0x11, 0x22];
            byte[] data = [..garbage,..SpecFrame];
            var buffer = new ReadOnlySequence<byte>(data);

            Assert.True(FrameCodec.TryParse(ref buffer, out Frame frame));
            Assert.Equal(SpecPayload, frame.Payload.ToArray());
            Assert.True(buffer.IsEmpty);
        }

        [Fact]
        public void TryParse_CorruptedFrameThenValid_ReturnsOnlyValid()
        {
            byte[] corrupted = [..SpecFrame];
            corrupted[10] ^= 0x01;
            byte[] data = [..corrupted, ..SpecFrame];
            var buffer = new ReadOnlySequence<byte>(data);

            Assert.True(FrameCodec.TryParse(ref buffer, out Frame frame));
            Assert.Equal(SpecPayload, frame.Payload.ToArray());

            Assert.False(FrameCodec.TryParse(ref buffer, out Frame nonValid));
            Assert.True(buffer.IsEmpty);

        }
    }
}