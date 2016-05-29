using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Server
{
    public class FrameSnapshot
    {
        public int ListIndex;
        public int TickCount;
        public FrameSnapshotEntry[] Entities;
        public int NumEntities;

        private int _refrences;

        public FrameSnapshot() {
            _refrences = 0;
        }

        ~FrameSnapshot() {
            Array.Clear(Entities, 0, Entities.Length);
            Debug.Assert(_refrences == 0);
        }


        public void AddRefrence() {
            Debug.Assert(_refrences < 0xFFFF );
            ++_refrences;
        }

        public void ReleaseRefrence() {
            Debug.Assert(_refrences > 0);

            --_refrences;
            if( _refrences == 0 ) {
                Program.FrameSnapshotManager.DeleteFrameSnapshot(this);
            }
        }

        public FrameSnapshot NextSnapshot() {
            return Program.FrameSnapshotManager.NextSnapshot(this);
        }
    }
}
