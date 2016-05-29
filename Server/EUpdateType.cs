using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public enum EUpdateType
    {
        /// <summary>
        /// Entity came back info pvs, create new entity if one doesn't exist
        /// </summary>
        EnterPVS = 0,

        /// <summary>
        /// Entit left pvs
        /// </summary>
        LeavePVS,

        /// <summary>
        /// There is a delta for this entity
        /// </summary>
        DeltaEnt,

        /// <summary>
        /// Entity stays alive but no delta
        /// </summary>
        PreserveEnt,

        //Finished parsing entities successfully
        Finished,

        /// <summary>
        /// Parsing error occured while readign entities
        /// </summary>
        Failed
    }
}
