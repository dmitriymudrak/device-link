using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace DeviceLink.Protocol
{
    /// <summary>Command codes the ground station sends to the drone.</summary>
    public enum CommandCode : byte
    {
        /// <summary>Hover / pause movement.</summary>
        Hold = 0x01,
        /// <summary>Resume movement after Hold.</summary>
        Resume = 0x02,
        /// <summary>Destroy (self-destruct) the drone.</summary>
        Destroy = 0x03
    }
    /// <summary>Drone command: id, command code and optional argument.</summary>
    public readonly record struct Command(ushort CommandId, CommandCode Code, ushort Arg)
    {
        /// <summary>Frame type code for a command.</summary>
        public const byte MessageType = 0x01;
        /// <summary>Payload size in bytes: id (u16) + code (u8) + argument (u16).</summary>
        public const int Size = 5;

        /// <summary>Serializes the command into 5 big-endian bytes.</summary>
        public void WriteTo(Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, CommandId);
            destination[2] = (byte)Code;
            BinaryPrimitives.WriteUInt16BigEndian(destination[3..], Arg);
        }

        /// <summary>Deserializes a command from a frame payload; false if the size does not match.</summary>
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

        /// <summary>Writes the command as a complete protocol frame with the given sequence number.</summary>
        public void WriteFrame(IBufferWriter<byte> writer, byte sequence)
        {
            Span<byte> payload = stackalloc byte[Size];
            WriteTo(payload);
            FrameCodec.Write(writer, MessageType, sequence, payload);
        }
    }
}
