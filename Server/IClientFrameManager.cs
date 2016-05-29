using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public interface IClientFrameManager
    {
        ClientFrame GetClientFrame(int tick, bool exact);
        int AddClientFrame(ClientFrame frame);
        int CountClientFrames();
        void RemoveOldestFrame();
        void DeleteClientFrames(int tick);
    }
}
