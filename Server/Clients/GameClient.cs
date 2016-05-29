using Shared;
using Shared.Enums;

namespace Server.Clients
{
    public class GameClient : BaseClient, IClientFrameManager
    {
        private ClientFrame _frames;
        public ClientFrame CurrentFrame;

        public GameClient(int slot, BaseServer server) : base(server) {
            Clear();

            EntityIndex = slot + 1;
            _frames = null;
            Server = server;
        }

        public override void ConnectionClosing(string reason) {
            Disconnect(reason ?? "Connection closing");
        }

        public override void Disconnect(string format, params object[] args) {
            if ( SignonState == ESignonState.None ) {
                return;
            }

            Server.RemoveClientFromGame(this);
            base.Disconnect(format, args);
        }

        public override bool SetSignonState(ESignonState state) {
            if ( state == ESignonState.Connected ) {
                if ( !CheckConnect() ) {
                    return false;
                }

                NetChannel.SetTimeout( Networking.SignonTimeout );
            } else if ( state == ESignonState.Full) {
                NetChannel.SetTimeout( Program.SvTimeout );
            }

            return base.SetSignonState(state);
        }

        public override bool SendSignonData() {
            // TODO: Check server class tables

            if ( !base.SendSignonData()) {
                return false;
            }

            return true;
        }

        public override void SendSnapshot( ClientFrame frame ) {
            WriteViewAngleUpdate();

            base.SendSnapshot( frame );
        }

        public void WriteViewAngleUpdate() {
            // TODO: send the current viewpos offset from the view entity
        }

        public bool CheckConnect() {
            var rejectReason = "Connection rejected by game";
            if( !Program.ServerPluginHandler.ClientConnect(this, GetClientName(), NetChannel.GetRemoteAddress(), ref rejectReason) ) {
                Disconnect(rejectReason);
                return false;
            }

            return true;
        }

        public override void ActivatePlayer() {
            base.ActivatePlayer();

            Program.ServerPluginHandler.ClientActive(this);
            // TODO: Player active game event
        }

        public override void Reconnect() {
            Server.RemoveClientFromGame(this);
            base.Reconnect();
        }

        public ClientFrame GetSendFrame() {
            return CurrentFrame;
        }

        public void SetupPackInfo( FrameSnapshot snapshot ) {
            // TODO: Setup PVS visibility

            CurrentFrame = new ClientFrame();
            CurrentFrame.Init( snapshot );

            var maxFrames = ClientFrame.MaxClientFrames;
            if( maxFrames < AddClientFrame(CurrentFrame) ) {
                RemoveOldestFrame();
            }
        }

        public ClientFrame GetClientFrame(int tick, bool exact) {
            if( tick < 0 ) {
                return null;
            }

            var frame = _frames;
            var lastFrame = frame;

            while( frame != null ) {
                if( frame.TickCount >= tick ) {
                    if( frame.TickCount == tick ) {
                        return frame;
                    }

                    if( exact ) {
                        return null;
                    }

                    return lastFrame;
                }

                lastFrame = frame;
                frame = frame.NextFrame;
            }

            if( exact ) {
                return null;
            }

            return lastFrame;
        }

        public int AddClientFrame(ClientFrame frame) {
            if( _frames == null ) {
                _frames = frame;
                return 1;
            }

            var count = 1;
            var f = _frames;
            while( f.NextFrame != null ) {
                f = f.NextFrame;
                ++count;
            }

            ++count;
            f.NextFrame = frame;

            return count;
        }

        public int CountClientFrames() {
            var count = 0;
            var f = _frames;
            while( f != null ) {
                ++count;
                f = f.NextFrame;
            }

            return count;
        }

        public void RemoveOldestFrame() {
            var frame = _frames;
            if( frame == null ) {
                return;
            }

            _frames = frame.NextFrame;
        }

        public void DeleteClientFrames(int tick) {
            var frame = _frames;
            ClientFrame prev = null;

            while( frame != null ) {
                if( tick < 0 || frame.TickCount < tick ) {
                    // then remove frame

                    if( prev != null ) {
                        prev.NextFrame = frame.NextFrame;
                        frame = prev.NextFrame;
                    } else {
                        _frames = frame.NextFrame;
                        frame = _frames;
                    }
                } else {
                    prev = frame;
                    frame = frame.NextFrame;
                }
            }
        }
    }
}
