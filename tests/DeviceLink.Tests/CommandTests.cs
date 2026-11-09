using DeviceLink.Protocol;
using System.Buffers;

namespace DeviceLink.Tests
{
    public class CommandTests
    {
        private static readonly byte[] SpecFrame =
            [0xAA, 0x55, 0x00, 0x05, 0x10, 0x00,
         0x00, 0x01, 0x03, 0x00, 0x00,
         0x48, 0x53];

        [Fact]
        public void WriteFrame_MatchesSpec()
        {
            var writer = new ArrayBufferWriter<byte>();
            new Command(1, CommandCode.Destroy, 0).WriteFrame(writer, sequence: 0);
            Assert.Equal(SpecFrame, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public void ParseThenRead_ReturnsCommand()
        {
            var buffer = new ReadOnlySequence<byte>(SpecFrame);
            Assert.True(FrameCodec.TryParse(ref buffer, out var frame));
            Assert.Equal(Command.MessageType, frame.Type);
            Assert.True(Command.TryRead(frame.Payload, out var command));
            Assert.Equal(new Command(1, CommandCode.Destroy, 0), command);
        }
    }
}
