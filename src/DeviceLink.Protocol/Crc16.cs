namespace DeviceLink.Protocol
{
    /// <summary>CRC-16/CCITT-FALSE checksum (polynomial 0x1021, initial 0xFFFF).</summary>
    public static class Crc16
    {
        private const ushort Polynomial = 0x1021;
        private const ushort InitialValue = 0xFFFF;

        /// <summary>Computes CRC-16 over the given bytes (bitwise, MSB first).</summary>
        public static ushort Compute(ReadOnlySpan<byte> input)
        {
            ushort crc = InitialValue;

            foreach (byte b in input)
            {
                crc ^= (ushort)(b << 8);

                for(int bit = 0; bit < 8; bit++)
                {
                    bool highBitSet = (crc & 0x8000) != 0;
                    crc <<= 1;
                    if(highBitSet)
                        crc ^= Polynomial;
                }
            }

            return crc;
        }
    }
}
