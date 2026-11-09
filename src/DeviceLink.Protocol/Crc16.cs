namespace DeviceLink.Protocol
{
    public static class Crc16
    {
        private const ushort Polynomial = 0x1021;
        private const ushort InitialValue = 0xFFFF;

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
