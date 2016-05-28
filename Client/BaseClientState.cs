using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;
using Shared.Buffers;
using Shared.Channel;
using Shared.Enums;
using Shared.Messages;
using Shared.NetMessages;

namespace Client
{
    public class BaseClientState : INetChannelHandler, IConnectionlessPacketHandler, IServerMessageHandler
    {
        public NetChannel NetChannel;
        public ESignonState SignonState;

        private Socket _socket;

        private int _challengeNr;
        private int _retryNumber;
        private float _connectTime;
        private string _retryAddress;

        public BaseClientState() {
            NetChannel = null;
            _socket = null;
            _challengeNr = 0;
            _retryNumber = 0;
            _retryAddress = null;
            SignonState = ESignonState.None;
        }

        public void Clear() {
            if ( NetChannel != null ) {
                NetChannel.Reset();
            }

            _challengeNr = 0;
            _connectTime = 0.0f;
        }

        public void Initialize(Socket socket) {
            _socket = socket;
        }

        public void Connect(string address) {
            _retryNumber = 0;
            _retryAddress = address;

            SetSignonState( ESignonState.Challenge );
        }

        public virtual void ConnectionStart(NetChannel netChannel) {
            netChannel.RegisterMessage(new NetMessageTick { ChannelHandler = this });
            netChannel.RegisterMessage(new NetMessageSignonState { ChannelHandler = this });
        }

        public virtual void ConnectionCrashed(string reason) {
            Console.WriteLine("Connection lost: {0}", reason ?? "unknown reason");
            Disconnect();
        }

        public virtual void ConnectionClosing(string reason) {
            Console.WriteLine("Disconnect: {0}", reason ?? "unknown reason");
            Disconnect();
        }

        public virtual void FileReceived(string fileName, uint transferId) {
            Console.WriteLine("BaseClientState.FileReceived: {0}", fileName);
        }

        public virtual void FileDenied(string fileName, uint transfterId) {
            Console.WriteLine("BaseClientState.FileDenied: {0}", fileName);
        }

        public virtual void FileRequested(string fileName, uint transferId) {
            Console.WriteLine("File '{0}' requested from {1}", fileName, NetChannel.GetRemoteAddress());

            NetChannel.SendFile( fileName, transferId );
        }

        public virtual void PacketStart(int inSequence, int outAcknowledged) { }
        public virtual void PacketEnd() { }

        public virtual void RunFrame( double time ) {
            if ( SignonState == ESignonState.Challenge ) {
                CheckForResend();
            }

            if( NetChannel != null ) {
                if (NetChannel.IsTimingOut()) {
                    Console.Title = string.Format("Timing out: {0:####.##}", NetChannel.GetTimeoutSeconds() - NetChannel.GetTimeSinceLastReceived());
                }

                if (NetChannel.IsTimedOut()) {
                    Console.WriteLine("Lost connection to the server.");
                    Disconnect();
                }
            }
        }

        private void SendConnectPacket(int challengeNr, int authProtocol) {
            var serverEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            if( !Networking.StringToAddr(_retryAddress, ref serverEp) ) {
                Console.WriteLine("Bad server address ({0})!\n", serverEp);
                Disconnect();
                return;
            }

            var msg = new BufferWrite();

            msg.WriteBytes(NetProtocol.ConnectionlessHeader);
            msg.WriteChar((char)EConnectionType.PlayerConnect);
            msg.WriteInt(Program.ProtocolVersion);
            msg.WriteInt(authProtocol);
            msg.WriteInt(challengeNr);
            msg.WriteString(Program.ClientName);
            msg.WriteString(Program.ServerPassword);

            Networking.SendPacket(null, _socket, serverEp, msg.GetData());
        }

        public virtual bool ProcessConnectionlessPacket(NetPacket packet) {
            var msg = packet.Message;

            var type = (EConnectionType)msg.ReadChar();
            switch( type ) {
                case EConnectionType.ConnectionAccept:
                    if ( SignonState == ESignonState.Challenge ) {
                        FullConnect(packet.From);
                    }
                    break;
                case EConnectionType.ServerChallenge:
                    if (SignonState == ESignonState.Challenge) {
                        var challengeNr = msg.ReadInt();
                        var authProtocol = msg.ReadInt();

                        SendConnectPacket(challengeNr, authProtocol);
                    }
                    break;
                case EConnectionType.ConnectionReject:
                    var reason = msg.ReadString();
                    Console.WriteLine("Disconnected from the server: {0}", reason);
                    Disconnect();
                    break;
                default:
                    Console.WriteLine("Bad connectionless packet ( CL '{0}' ) from {1}.\n", type, packet.From);
                    return false;
            }

            return true;
        }

        public virtual void FullConnect(EndPoint addr) {
            NetChannel = Networking.CreateChannel(_socket, "CLIENT", addr, this);
            SetSignonState( ESignonState.Connected );
        }

        private void CheckForResend() {
            if( Networking.NetTime - _connectTime < Program.ClResend ) {
                return;
            }

            var serverEp = (EndPoint) new IPEndPoint(IPAddress.Any, 0);
            if( !Networking.StringToAddr(_retryAddress, ref serverEp) ) {
                Console.WriteLine("Bad server address ({0})!\n", serverEp);
                Disconnect();
                return;
            }

            // only retry so many times before failure
            if( _retryNumber >= Networking.ConnectionRetries ) {
                Console.WriteLine("Connection failed after {0} retries.\n", Networking.ConnectionRetries);
                Disconnect();
                return;
            }

            _connectTime = (float)Networking.NetTime;

            if( _retryNumber == 0 ) {
                Console.WriteLine("Connecting to {0} ...", serverEp);
            } else {
                Console.WriteLine("Retrying {0} ...", serverEp);
            }

            _retryNumber++;

            Networking.OutOfBandPrintf(_socket, serverEp, "{0}00000000000000", (char)EConnectionType.GetChallenge);
        }

        public virtual void Disconnect( bool showMainMenu = false ) {
            if ( SignonState == ESignonState.None ) {
                return;
            }

            SignonState = ESignonState.None;

            if( NetChannel != null ) {
                NetChannel.Shutdown("Disconnect by user.");
                NetChannel = null;
            }
        }

        public bool SetSignonState(ESignonState state) {
            if ( state < ESignonState.None || state > ESignonState.Changelevel ) {
                Console.WriteLine("Received signon {0} when at {1}.", state, SignonState);
                return false;
            }

            Console.WriteLine("Setting signon to {0}", state);
            SignonState = state;
            return true;
        }

        public virtual bool ProcessTick(NetMessageTick msg) {
            NetChannel.SetRemoteFramerate(msg.HostFrameTime, msg.HostFrameTimeStdDeviation);

            // TODO: Set client tick count
            // TODO: Set server tick count

            // TODO: return GetServerTickCount() > 0
            return true;
        }

        public virtual bool ProcessSignonState(NetMessageSignonState msg) {
            return SetSignonState(msg.SignonState);
        }
    }
}
