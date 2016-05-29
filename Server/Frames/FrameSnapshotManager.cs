using System.Collections.Generic;

namespace Server.Frames
{
    public class FrameSnapshotManager
    {
        private readonly LinkedList<FrameSnapshot> _frameSnapshots;

        public FrameSnapshotManager() {
            _frameSnapshots = new LinkedList<FrameSnapshot>();
        }

        public FrameSnapshot NextSnapshot( FrameSnapshot snapshot ) {
            if( snapshot == null ) {
                return null;
            }

            var next = _frameSnapshots.Find(snapshot);
            if( next == null || next.Next == null ) {
                return null;
            }

            return next.Next.Value;
        }

        public FrameSnapshot CreateEmptySnapshot( int tickcount, int maxEntities ) {
            var snap = new FrameSnapshot();
            snap.AddRefrence();
            snap.TickCount = tickcount;
            snap.Entities = new FrameSnapshotEntry[maxEntities];

            var entry = snap.Entities;
            for (var i = 0; i < maxEntities; i++) {
                entry[i].SerialNumber = -1;
                i++;
            }

            _frameSnapshots.AddLast(snap);
            return snap;
        }

        public FrameSnapshot TakeTickSnapshot( int tickcount ) {
            //var snap = CreateEmptySnapshot(tickcount, Program.Server)
            return null;
        }

        public void DeleteFrameSnapshot(FrameSnapshot frameSnapshot) {
            _frameSnapshots.Remove(frameSnapshot);
        }
    }
}
