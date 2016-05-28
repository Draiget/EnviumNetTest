using Shared.Buffers;
using Shared.Channel;
using Shared.Enums;

namespace Shared.Messages
{
    public interface INetMessage
    {
        void SetNetChannel(NetChannel channel);
        bool Process();
        bool IsReliable();
        void SetReliable( bool state );

        bool WriteToBuffer(BufferWrite buffer);
        bool ReadFromBuffer(BufferRead buffer);

        ENetCommand GetMsgType();
        string GetName();
    }
}
