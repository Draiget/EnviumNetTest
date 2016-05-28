using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.Messages;

namespace Shared
{
    public interface IConnectionlessPacketHandler
    {
        bool ProcessConnectionlessPacket( NetPacket packet );
    }
}
