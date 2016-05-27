using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared
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
