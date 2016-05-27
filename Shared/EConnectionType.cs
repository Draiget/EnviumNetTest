using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared
{
    public enum EConnectionType
    {
        RequestInfo = 'Y',

        /// <summary>
        /// Request challenge # from another machine
        /// </summary>
        GetChallenge = 'K',

        /// <summary>
        /// Server responde to client challenge request
        /// + challenge value
        /// </summary>
        ServerChallenge = 'A',

        /// <summary>
        /// Connectin request by client
        /// </summary>
        PlayerConnect = 'k',

        /// <summary>
        /// Connection was rejected
        /// </summary>
        ConnectionReject = '1',

        /// <summary>
        /// Connection has accepted
        /// </summary>
        ConnectionAccept = 'B',
    }
}
