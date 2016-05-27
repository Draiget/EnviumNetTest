using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared;

namespace Server
{
    public interface IClient
    {
        void Connect(string name, NetChannel channel);
        void Disconnect(string format, params object[] args);

        void Clear();
    }
}
