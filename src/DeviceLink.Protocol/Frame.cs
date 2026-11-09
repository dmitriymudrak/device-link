using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceLink.Protocol
{
    public readonly struct Frame
    {
        public Frame(byte type, byte sequence, ReadOnlySequence<byte> payload)
        {
            Type = type;
            Sequence = sequence;
            Payload = payload;
        }

        public byte Type { get; }
        public byte Sequence { get; }
        public ReadOnlySequence<byte> Payload { get; }
    }
}
