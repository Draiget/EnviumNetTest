using System.Net;
using Shared.Buffers;

namespace Shared.Messages
{
    public class NetPacket
    {
        public EndPoint From;
        public BufferRead Message;

        public NetPacket() {
            From = new IPEndPoint(IPAddress.Any, 0);
            Message = null;
        }

        public void Assign(EndPoint from, int len, byte[] data) {
            From = from;
            Message = new BufferRead(data, len);
        }

        public bool HasData {
            get { return Data.Length > 0; }
        }

        public byte[] Data {
            get { return Message.GetData(); }
        }
    }
}
