using System;
using Shared;
using Shared.Buffers;
using Shared.Channel;
using Shared.Enums;
using Shared.NetMessages;

namespace Server.Clients
{
    public class BaseClient : INetChannelHandler, IClient, IClientMessageHandler
    {
        private string _clientName;
        private NetChannel _netChannel;
        private bool _sendServerInfo;
        private bool _conVarsChanged;
        private bool _receivedPacket;
        private FrameSnapshot _lastSnapshot;
        private int _forceWaitForTick;
        private int _deltaTick;

        public FrameSnapshot Baseline;
        public int BaselineUsed;
        public double NextMessageTime;
        public float SnapshotInterval;
        public int BaselineUpdateTick;

        public int EntityIndex;

        protected ESignonState SignonState;
        protected BaseServer Server;

        public BaseClient(BaseServer server) {
            Server = server;
            SignonState = ESignonState.None;
            _sendServerInfo = false;
            _conVarsChanged = false;
            _lastSnapshot = null;
            _forceWaitForTick = -1;
            _deltaTick = -1;
            BaselineUsed = 0;
            Baseline = null;
        }

        public void SetRate(int rate) {
            if ( _netChannel != null ) {
                _netChannel.SetDataRate(rate);
            }
        }

        public int GetRate() {
            if (_netChannel == null) {
                return 0;
            }

            return _netChannel.GetDataRate();
        }

        public void FreeBaselines() {
            if( Baseline != null ) {
                Baseline.ReleaseRefrence();
                Baseline = null;
            }

            BaselineUpdateTick = -1;
            BaselineUsed = 0;
        }

        public void ConnectionStart(NetChannel netChannel) {
            netChannel.RegisterMessage(new NetMessageTick { ChannelHandler = this });
            netChannel.RegisterMessage(new NetMessageSignonState { ChannelHandler = this });
        }

        public virtual void ConnectionCrashed(string reason) { }
        public virtual void ConnectionClosing(string reason) { }

        public virtual void FileReceived(string fileName, uint transferId) { }
        public virtual void FileDenied(string fileName, uint transfterId) { }
        public virtual void FileRequested(string fileName, uint transferId) { }

        public virtual void PacketStart(int inSequence, int outAcknowledged) { }
        public virtual void PacketEnd() { }

        public void Connect(string name, NetChannel channel) {
            _clientName = name;
            _netChannel = channel;

            SignonState = ESignonState.Connected;
        }

        public virtual void Reconnect() {
            _netChannel.Clear();

            SignonState = ESignonState.Connected;
            var signon = new NetMessageSignonState( SignonState );
            _netChannel.SendNetMsg(signon);
        }

        public virtual void Disconnect(string format, params object[] args) {
            if ( SignonState == ESignonState.None ) {
                return;
            }

            SignonState = ESignonState.None;

            var reason = string.Format(format, args);
            Console.WriteLine("Dropped \"{0}\" from server: {1}", _clientName, reason);

            if ( _netChannel != null ) {
                _netChannel.Shutdown(reason);
                _netChannel = null;
            }

            Clear();
        }

        public virtual bool SetSignonState(ESignonState state) {
            switch (state) {
                case ESignonState.Connected:
                    // client is connected, leave client in this state
                    _sendServerInfo = true;
                    break;
                case ESignonState.New:
                    if ( !SendSignonData() ) {
                        return false;
                    }

                    break;
                case ESignonState.PreSpawn:
                    SpawnPlayer();
                    break;
                case ESignonState.Spawn:
                    ActivatePlayer();
                    break;
                case ESignonState.Full:
                    break;
            }

            return true;
        }

        public virtual bool SendSignonData() {
            SignonState = ESignonState.PreSpawn;

            _netChannel.SendData(Server.Signon);
            var signonState = new NetMessageSignonState( SignonState );

            return _netChannel.SendNetMsg( signonState );
        }

        public virtual void SpawnPlayer() {
            var tick = new NetMessageTick( Server.GetTick(), Program.HostFrametimeUnbounded, Program.HostFrametimeStdDeviation );
            _netChannel.SendNetMsg(tick, true);

            SignonState = ESignonState.Spawn;
            var signonState = new NetMessageSignonState( SignonState );
            _netChannel.SendNetMsg( signonState );
        }
        
        public virtual void ActivatePlayer() {
            SignonState = ESignonState.Full;

            Server.UserInfoChanged(this);
        }

        public void Clear() {
            if( _netChannel != null ) {
                _netChannel.Shutdown("Disconnect by server.\n");
                _netChannel = null;
            }

            _clientName = string.Empty;
            SignonState = ESignonState.None;
            _sendServerInfo = false;
            _conVarsChanged = false;
            NextMessageTime = 0;
            _lastSnapshot = null;
            _forceWaitForTick = -1;
            _deltaTick = -1;
            BaselineUpdateTick = -1;
            BaselineUsed = 0;
        }

        public string GetClientName() {
            return _clientName;
        }

        public bool IsConnected() {
            return SignonState >= ESignonState.Connected;
        }

        public bool IsActive() {
            return SignonState == ESignonState.Full;
        }

        public bool IsSpawned() {
            return SignonState >= ESignonState.New;
        }

        public NetChannel NetChannel {
            get { return _netChannel; }
        }

        public bool HasConvarsChanged() {
            return _conVarsChanged;
        }

        public bool HasWaitSendServerInfo() {
            return _sendServerInfo;
        }

        public bool SendServerInfo() {
            var msg = new BufferWrite();

            _sendServerInfo = false;
            SignonState = ESignonState.New;

            var signonMsg = new NetMessageSignonState( SignonState );
            signonMsg.WriteToBuffer(msg);

            if( !_netChannel.SendData(msg) ) {
                Disconnect( "Server info data overflow" );
                return false;
            }

            return true;
        }

        public bool ProcessTick(NetMessageTick msg) {
            _netChannel.SetRemoteFramerate(msg.HostFrameTime, msg.HostFrameTimeStdDeviation);
            return UpdateAcknowledgedFramecount(msg.Tick);
        }

        public bool ProcessSignonState(NetMessageSignonState msg) {
            if ( msg.SignonState == ESignonState.Changelevel ) {
                return true;
            }

            if ( msg.SignonState > ESignonState.Connected ) {
                // TODO :Check respan count and reconnect is they not match
            }

            if ( msg.SignonState != SignonState ) {
                Reconnect();
                return true;
            }

            return SetSignonState( msg.SignonState );
        }

        private bool UpdateAcknowledgedFramecount(int tick) {
            // TODO: Pending reliable data
            return true;
        }

        public void UpdateUserSettings() {
            // SetRate();
            // TODO: Setup rate from client convars

            Server.UserInfoChanged(this);
            _conVarsChanged = true;
        }

        public virtual void SendSnapshot( ClientFrame frame ) {
            // do not send same snapshot twice
            if ( _lastSnapshot == frame.GetSnapshot() ) {
                NetChannel.Transmit();
                return;
            }

            // if we send a full snapshot (no delta-compression) before, wait until client
            // received and acknowledge that update. don't spam client with full updates
            if( _forceWaitForTick > 0 ) {
                NetChannel.Transmit();
                return;
            }

            var msg = new BufferWrite();

            var deltaFrame = GetDeltaFrame( _deltaTick );
            if( deltaFrame == null ) {
                // We need to send a full update and reset the instanced baselines
                OnRequestFullUpdate();
            }

            var tickmsg = new NetMessageTick( frame.TickCount, Program.HostFrametimeUnbounded, Program.HostFrametimeStdDeviation );
            tickmsg.WriteToBuffer(msg);

            // send entity update, delta compressed if deltaFrame != NULL
            Server.WriteDeltaEntities( this, frame, deltaFrame, msg );

            var maxTempEnts = 255;
            Server.WriteTempEntities( this, frame.GetSnapshot(), _lastSnapshot, msg, maxTempEnts );

            _lastSnapshot = frame.GetSnapshot();

            if ( NetChannel == null ) {
                _deltaTick = frame.TickCount;
                return;
            }

            bool sendOk;

            if (deltaFrame == null) {
                sendOk = NetChannel.SendData(msg);
                sendOk = sendOk && NetChannel.Transmit();

                // remember this tickcount we send the reliable snapshot
                // so we can continue sending other updates if this has been acknowledged
                _forceWaitForTick = frame.TickCount;
            } else {
                sendOk = NetChannel.SendDatagram( msg ) > 0;
            }

            if ( !sendOk ) {
                Disconnect("Error! Couldn't send snapshot.");
            }
        }

        public void OnRequestFullUpdate() {
            // client requests a full update 
            _lastSnapshot = null;

            // free old baseline snapshot
            FreeBaselines();

            Baseline = Program.FrameSnapshotManager.CreateEmptySnapshot(0, 1 << 11);

            Console.WriteLine("Sending full update to client \"{0}\"", GetClientName());
        }

        public ClientFrame GetDeltaFrame( int tick ) {
            return null;
        }

        public void UpdateSendState() {
            if (IsActive()) {
                // multiplayer mode
                var maxDelta = Utils.Min(Program.GetTickInterval(), SnapshotInterval);
                var delta = Utils.Clamp(Networking.NetTime - NextMessageTime, 0.0f, maxDelta);
                NextMessageTime = Networking.NetTime + SnapshotInterval - delta;
            } else {
                // signon mode
                if (NetChannel != null && NetChannel.HasPendingReliableData() && NetChannel.GetTimeSinceLastReceived() < 1.0f) {
                    NextMessageTime = Networking.NetTime;
                } else {
                    NextMessageTime = Networking.NetTime + 1.0f;
                }
            }
        }

        public void SetUpdateRate( int updateRate ) {
            updateRate = Utils.Clamp(updateRate, 1, 100);
            SnapshotInterval = 1.0f / updateRate;
        }

        public int GetUpdateRate() {
            if( SnapshotInterval > 0 ) {
                return (int)( 1.0f / SnapshotInterval );
            }

            return 0;
        }

        public bool ShouldSendMessage() {
            if ( !IsConnected() ) {
                return false;
            }

            var sendMessage = NextMessageTime <= Networking.NetTime;
            if( !sendMessage && !IsActive() ) {
                if( _receivedPacket && NetChannel != null && NetChannel.HasPendingReliableData() ) {
                    sendMessage = true;
                }
            }

            if ( sendMessage && NetChannel != null && !NetChannel.CanPacket() ) {
                NetChannel.SetChoked();
                sendMessage = false;
            }

            return sendMessage;
        }
    }
}
