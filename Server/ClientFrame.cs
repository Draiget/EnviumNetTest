namespace Server
{
    public class ClientFrame
    {
        public static readonly int MaxClientFrames = 128;

        public int TickCount;
        public ClientFrame NextFrame;

        private FrameSnapshot _snapshot;

        public ClientFrame(int tickcount) {
            TickCount = tickcount;
            _snapshot = null;
            NextFrame = null;
        }

        public ClientFrame() {
            TickCount = 0;
            _snapshot = null;
            NextFrame = null;
        }

        public void Init(int tickcount) {
            TickCount = tickcount;
        }

        public void Init(FrameSnapshot snapshot) {
            TickCount = snapshot.TickCount;
            SetSnapshot(snapshot);
        }

        public void SetSnapshot(FrameSnapshot snapshot) {
            if( _snapshot == snapshot ) {
                return;
            }

            if( snapshot != null ) {
                snapshot.AddRefrence();
            }

            if ( _snapshot != null ) {
                _snapshot.ReleaseRefrence();
            }

            _snapshot = snapshot;
        }

        public void CopyFrame(ClientFrame frame) {
            TickCount = frame.TickCount;

            SetSnapshot( frame.GetSnapshot() );
        }

        public FrameSnapshot GetSnapshot() {
            return _snapshot;
        }
    }
}
