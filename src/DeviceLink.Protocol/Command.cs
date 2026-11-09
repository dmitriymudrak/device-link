using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace DeviceLink.Protocol
{
    public enum CommandCode : byte
    {
        Hold = 0x01,
        Resume = 0x02,
        Destroy = 0x03
    }
    public readonly record struct Command(ushort CommandId, CommandCode Code, ushort Arg)
    {
        public const byte MessageType = 0x01;
        public const int Size = 5;

        public void WriteTo(Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, CommandId);
            destination[2] = (byte)Code;
            BinaryPrimitives.WriteUInt16BigEndian(destination[3..], Arg);
        }

        public static bool TryRead(ReadOnlySequence<byte> payload, out Command command)
        {
            command = default;
            if (payload.Length != Size) return false;

            Span<byte> buffer = stackalloc byte[Size];
            payload.CopyTo(buffer);

            command = new Command(
                BinaryPrimitives.ReadUInt16BigEndian(buffer),
                (CommandCode)buffer[2],
                BinaryPrimitives.ReadUInt16BigEndian(buffer[3..]
                ));

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
