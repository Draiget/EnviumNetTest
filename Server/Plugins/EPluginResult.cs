using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Plugins
{
    public enum EPluginResult
    {
        /// <summary>
        /// Keep going
        /// </summary>
        Continue = 0,

        /// <summary>
        /// Run game function, but override return value instead
        /// </summary>
        Override,

        /// <summary>
        /// Don't run game function, use own at all
        /// </summary>
        Stop
    }
}
