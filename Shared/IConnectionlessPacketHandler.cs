using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared
{
    public interface IConnectionlessPacketHandler
    {
        bool ProcessConnectionlessPacket( NetPacket packet );
    }
}
