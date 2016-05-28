using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Shared;
using Shared.Buffers;
using Shared.Channel;
using Shared.Enums;
using Shared.NetMessages;

namespace Server
{
    public class BaseClient : INetChannelHandler, IClient, IClientMessageHandler
    {
        private string _clientName;
        private NetChannel _netChannel;
        private ESignonState _signonState;
        private bool _sendServerInfo;
        private bool _conVarsChanged;
        private BaseServer _server;

        public BaseClient(BaseServer server) {
            _server = server;
            _signonState = ESignonState.None;
            _sendServerInfo = false;
            _conVarsChanged = false;
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

        public void ConnectionStart(NetChannel netChannel) {
            netChannel.RegisterMessage(new NetMessageTick { ChannelHandler = this });
            netChannel.RegisterMessage(new NetMessageSignonState { ChannelHandler = this });
        }

        public void ConnectionCrashed(string reason) { }
        public void ConnectionClosing(string reason) { }

        public void FileReceived(string fileName, uint transferId) { }
        public void FileDenied(string fileName, uint transfterId) { }
        public void FileRequested(string fileName, uint transferId) { }

        public void PacketStart(int inSequence, int outAcknowledged) { }
        public void PacketEnd() { }

        public void Connect(string name, NetChannel channel) {
            _clientName = name;
            _netChannel = channel;

            _signonState = ESignonState.Connected;
        }

        public void Reconnect() {
            _netChannel.Clear();

            _signonState = ESignonState.Connected;
            var signon = new NetMessageSignonState( _signonState );
            _netChannel.SendNetMsg(signon);
        }

        public void Disconnect(string format, params object[] args) {
            var reason = string.Format(format, args);
            Console.WriteLine("Dropped \"{0}\" from server: {1}", _clientName, reason);

            if ( _netChannel != null ) {
                _netChannel.Shutdown(reason);
                _netChannel = null;
            }

            Clear();
        }

        public bool SetSignonState(ESignonState state) {
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

        private bool SendSignonData() {
            _signonState = ESignonState.PreSpawn;

            _netChannel.SendData(_server.Signon);
            var signonState = new NetMessageSignonState( _signonState );

            return _netChannel.SendNetMsg( signonState );
        }

        private void SpawnPlayer() {
            _signonState = ESignonState.Spawn;
            
            var tick = new NetMessageTick( _server.GetTick(), Program.HostFrametimeUnbounded, Program.HostFrametimeStdDeviation );
            _netChannel.SendNetMsg(tick, true);

            var signonState = new NetMessageSignonState( _signonState );
            _netChannel.SendNetMsg( signonState );
        }
        
        private void ActivatePlayer() {
            _signonState = ESignonState.Full;
        }

        public void Clear() {
            if( _netChannel != null ) {
                _netChannel.Shutdown("Disconnect by server.\n");
                _netChannel = null;
            }

            _clientName = string.Empty;
            _signonState = ESignonState.None;
            _sendServerInfo = false;
            _conVarsChanged = false;
        }

        public string GetClientName() {
            return _clientName;
        }

        public bool IsConnected() {
            return _signonState >= ESignonState.Connected;
        }

        public bool IsActive() {
            return _signonState == ESignonState.Full;
        }

        public bool IsSpawned() {
            return _signonState >= ESignonState.New;
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
            _signonState = ESignonState.New;

            var signonMsg = new NetMessageSignonState( _signonState );
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

            if ( msg.SignonState != _signonState ) {
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

            _server.UserInfoChanged(this);
            _conVarsChanged = true;
        }
    }
}
