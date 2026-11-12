using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceLink.Protocol
{
    /// <summary>Parsed protocol frame: message type, sequence number and payload.</summary>
    public readonly struct Frame
    {
        /// <summary>Creates a frame with the given type, sequence number and payload.</summary>
        public Frame(byte type, byte sequence, ReadOnlySequence<byte> payload)
        {
            Type = type;
            Sequence = sequence;
            Payload = payload;
        }

        /// <summary>Message type (header byte 4), e.g. Telemetry.MessageType.</summary>
        public byte Type { get; }
        /// <summary>Frame sequence number (header byte 5), wrapping 0..255.</summary>
        public byte Sequence { get; }
        /// <summary>Payload; references the input buffer, so it is valid only until that buffer is released.</summary>
        public ReadOnlySequence<byte> Payload { get; }
    }
}
