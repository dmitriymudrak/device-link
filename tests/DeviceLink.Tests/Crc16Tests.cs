using DeviceLink.Protocol;
using System.Text;

namespace DeviceLink.Tests
{
    public class Crc16Tests
    {
        [Fact]
        public void Compute_ReferenceString_ReturnsCheckValue()
        {
            var data = Encoding.ASCII.GetBytes("123456789");

            ushort crc = Crc16.Compute(data);

            Assert.Equal(0x29B1, crc);
        }

        [Fact]
        public void Compute_TelemetryFrameBody_MatchesSpecFrame()
        {
            byte[] body =
                [
                  0x00,0x08, //payload length
                  0x01, // type
                  0x00, // frame number
                  0x1E, 0x12, 0x13, 0x08, //longitude
                  0x12, 0x31, 0x80, 0x50 //latitude
                ];

            //check crccalc.com
            Assert.Equal(0x2E71, Crc16.Compute(body));
        }

    }
}
