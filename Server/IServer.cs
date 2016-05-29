using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public interface IServer
    {
        bool IsActive();
        bool IsLoading();
        bool IsPaused();
    }
}
