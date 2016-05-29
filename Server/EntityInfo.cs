using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public class EntityInfo
    {
        public bool AsDelta;
        public ClientFrame From;
        public ClientFrame To;

        public EUpdateType UpdateType;

        public int OldEntity;
        public int NewEntity;

        public int HeaderBase;
        public int HeaderCount;

        public EntityInfo() {
            OldEntity = -1;
            NewEntity = -1;
            HeaderBase = -1;
        }

        public void NextOldEntity() {
            
        }

        public void NextNewEntity() {
            
        }
    }
}
