using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Server.Clients;
using Server.Frames;
using Server.Util;
using Shared;
using Shared.Buffers;
using Shared.Enums;
using Shared.Messages;

namespace Server
{
    public class BaseServer : IServer
    {
        private const int MaxChallenges = 16384;
        private const float ChallengeLifetime = 60 * 60.0f;

        private List<NetChallenge> _serverQueryChallenges;

        protected readonly Socket Socket;
        protected List<GameClient> Clients;

        public BufferWrite Signon;
        public float TickInterval;
        public int TickCount;
        public EServerState State;

        public BaseServer(Socket serverSocket) {
            Socket = serverSocket;
            _serverQueryChallenges = new List<NetChallenge>();
            Clients = new List<GameClient>();
            Signon = new BufferWrite();
            TickInterval = 0.03f;
        }

        public virtual void RemoveClientFromGame(BaseClient client) { }

        public void ProcessConnectionlessPacket(NetPacket packet) {
            var msg = packet.Message;

            var type = (EConnectionType)msg.ReadChar();

            if( !CheckConnectionLessRateLimits(packet.From) ) {
                return;
            }

            switch( type ) {
                case EConnectionType.GetChallenge:
                    ReplyChallenge( packet.From );
                    break;
                case EConnectionType.RequestInfo:
                    // TODO: Responde player's info, map ect.
                    break;
                case EConnectionType.PlayerConnect:
                    var protocol = msg.ReadInt();
                    var authProtocol = msg.ReadInt();
                    var challengeNr = msg.ReadInt();

                    var playerName = msg.ReadString();
                    var serverPassword = msg.ReadString();

                    ConnectClient(packet.From, protocol, authProtocol, challengeNr, playerName, serverPassword);
                    break;
                default:
                    Out.Debug("Unknown connectionless type '{0}'!", type);
                    return;
            }
        }

        private void ConnectClient(EndPoint addr, int protocol, int authProtocol, int challenge, string name, string password) {
            if( name == null || password == null ) {
                return;
            }

            if( !CheckChallengeNr(addr, challenge) ) {
                RejectConnecton( addr, "Bad challenge.\n" );
                return;
            }

            if( !CheckIPConnectionReuse(addr) ) {
                RejectConnecton(addr, "Too many pending connections.\n");
                return;
            }

            if( !CheckIPRestrictions(addr) ) {
                RejectConnecton(addr, "LAN servers are resticted to local clients only (class C).\n");
                return;
            }

            if( !CheckPassword(addr, password) ) {
                Console.WriteLine("Client \"{0}\" [{1}]: password failed.\n", name, addr);
                RejectConnecton(addr, "Bad password.\n");
                return;
            }

            if( Clients.Count + 1 > Program.SvMaxPlayers ) {
                RejectConnecton(addr, "Server is full.\n");
                return;
            }

            var client = new GameClient( GetFreeSlot(), this);
            var channel = Networking.CreateChannel(Socket, name, addr, client);
            if( channel == null ) {
                RejectConnecton(addr, "Failed to create net channel!\n");
                return;
            }

            // set channel challenge
            channel.SetChallengeNr(challenge);

            // make sure client is reset and clear
            client.Connect(name, channel);

            client.SnapshotInterval = 1.0f / 20.0f;
            client.NextMessageTime = Networking.NetTime + client.SnapshotInterval;

            // add client to global list
            Clients.Add(client);

            // tell client connection worked, now use netchannels
            Networking.OutOfBandPrintf(Socket, addr, "{0}00000000000000", (char)EConnectionType.ConnectionAccept);

            Out.MsgC( ConsoleColor.Yellow, "Client \"{0}\" has connected from [{1}]", client.GetClientName(), channel.GetRemoteAddress());
        }

        private int GetFreeSlot() {
            if( Clients.Count == 0 ) {
                return 0;
            }

            for (var i = 0; i < Clients.Count; i++) {
                if ( Clients[i] == null ) {
                    return i;
                }   
            }

            return Clients.Count + 1 < Program.SvMaxPlayers ? Clients.Count + 1 : -1;
        }

        private void ReplyChallenge(EndPoint clientEp) {
            var msg = new BufferWrite();

            // get a free challenge number
            var challengeNr = GetChallengeNr( clientEp );
            
            msg.WriteBytes(NetProtocol.ConnectionlessHeader);

            msg.WriteChar((char)EConnectionType.ServerChallenge);
            msg.WriteInt(challengeNr);
            msg.WriteInt(1); // auth protocol
            msg.WriteString("EE00");

            Networking.SendPacket(null, Socket, clientEp, msg.GetData());
        }

        private bool CheckChallengeNr(EndPoint addr, int challenge) {
            for (var i=0; i < _serverQueryChallenges.Count; i++) {
                if ( _serverQueryChallenges[i].Addr.CompareAddr(addr, true) ) {
                    if (challenge != _serverQueryChallenges[i].Challenge) {
                        return false;
                    }

                    // allow challenge values to last for 1 hour
                    if( Networking.NetTime > _serverQueryChallenges[ i ].Time + ChallengeLifetime ) {
                        _serverQueryChallenges.RemoveAt(i);
                        Out.Warning("Old challenge from {0}", addr);
                        return false;
                    }

                    return true;
                }

                // clean up any old entries
                if( Networking.NetTime > _serverQueryChallenges[ i ].Time + ChallengeLifetime ) {
                    _serverQueryChallenges.RemoveAt(i);
                }
            }

            if( challenge != -1) {
                Out.Debug("No challenge from {0}", addr);
            }
            return false;
        }

        private int GetChallengeNr(EndPoint addr) {
            var oldest = 0;
            var oldestTime = float.MaxValue;
            for (var i=0; i < _serverQueryChallenges.Count; i++) {
                if( _serverQueryChallenges[ i ].Addr.CompareAddr(addr, true) ) {
                    // reuse challenge, and update time
                    _serverQueryChallenges[ i ].Time = (float)Networking.NetTime;
                    return _serverQueryChallenges[i].Challenge;
                }

                if( _serverQueryChallenges[ i ].Time < oldestTime ) {
                    oldestTime = _serverQueryChallenges[i].Time;
                    oldest = i;
                }
            }

            if( _serverQueryChallenges.Count > MaxChallenges ) {
                _serverQueryChallenges.RemoveAt(oldest);
            }

            // note the 0x0FFF of the top 16 bits, so that -1 will never be sent as a challenge
            var newChallenge = new NetChallenge {
                Challenge = Utils.RandomInt(0, 0x0FFF) << 16 | Utils.RandomInt(0, 0xFFFF),
                Addr = addr,
                Time = (float)Networking.NetTime
            };

            _serverQueryChallenges.Add(newChallenge);
            return newChallenge.Challenge;
        }

        private bool CheckConnectionLessRateLimits(EndPoint addr) {
            // TODO: Check connections for ip per second window
            return true;
        }

        private bool CheckIPConnectionReuse(EndPoint addr) {
            var sameConnections = Clients.Count(client => client.IsConnected() && !client.IsActive() && client.NetChannel.GetRemoteAddress().CompareAddr(addr, true));
            if( sameConnections > Networking.MaxReusePerIp ) {
                Out.Warning("Too many connect packets from {0}!", addr.ToString(true));
                return false;
            }

            return true;
        }

        private bool CheckIPRestrictions(EndPoint addr) {
            if ( Program.SvLan ) {
                var ip = addr as IPEndPoint;
                if (ip == null) {
                    return false;
                }

                if( IPAddress.IsLoopback(ip.Address) ) {
                    return true;
                }

                var localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                return localIPs.Contains(ip.Address);
            }

            return true;
        }

        private bool CheckPassword(EndPoint addr, string password) {
            var serverPassword = GetPassword();

            if( serverPassword == null ) {
                return true;
            }

            if (password.Length != serverPassword.Length) {
                return false;
            }

            return serverPassword == password;
        }

        private string GetPassword() {
            if( Program.SvPassword == null || Program.SvPassword == "none" ) {
                return null;
            }

            return Program.SvPassword;
        }

        public void RejectConnecton(EndPoint addr, string format, params object[] args) {
            var message = string.Format(format, args);
            Networking.OutOfBandPrintf(Socket, addr, "{0}{1}", (char)EConnectionType.ConnectionReject, message);
        }

        public void SendPendingServerInfo() {
            foreach (var client in Clients) {
                if ( client.HasWaitSendServerInfo() ) {
                    client.SendServerInfo();
                }
            }
        }

        public void RunFrame() {
            CheckTimeouts();
            UpdateUserSettings();
        }

        private void CheckTimeouts() {
            foreach (var client in Clients) {
                if ( !client.IsConnected() ) {
                    continue;
                }

                var netChannel = client.NetChannel;
                if ( netChannel == null ) {
                    continue;
                }

                if( netChannel.IsTimedOut() ) {
                    client.Disconnect("{0} timed out", client.GetClientName());
                }
            }
        }

        private void UpdateUserSettings() {
            foreach (var client in Clients) {
                if ( client.HasConvarsChanged() ) {
                    client.UpdateUserSettings();
                }
            }
        }

        public void BroadcastMessage(INetMessage msg, bool onlyActive = false, bool reliable = false) {
            foreach (var client in Clients) {
                if ( (onlyActive && !client.IsActive()) || !client.IsSpawned() ) {
                    continue;
                }

                if ( !client.NetChannel.SendNetMsg( msg, reliable ) ) {
                    if ( msg.IsReliable() || reliable ) {
                        Out.Warning("BaseServer.BroadcastMessage: Reliable broadcast message overflow for client {0}", client.GetClientName());
                    }
                }
            }
        }

        public void BroadcastMessage( INetMessage msg, IRecipientFilter filter ) {
            if( filter.IsInitMessage() ) {
                if ( !msg.WriteToBuffer( Signon ) ) {
                    Out.Warning("BaseServer.BroadcastMessage: Init message would overflow signon buffer!");
                }

                return;
            }

            msg.SetReliable( filter.IsReliable() );

            foreach (var client in Clients) {
                if ( !client.IsSpawned() ) {
                    continue;
                }

                if ( !client.NetChannel.SendNetMsg(msg) ) {
                    if ( msg.IsReliable() ) {
                        Out.Warning("BaseServer.BroadcastMessage: Reliable filter message overflow for client {0}", client.GetClientName());
                    }
                }
            }
        }

        public int GetTick() {
            return TickCount;
        }

        public void UserInfoChanged(BaseClient client) {
            // TODO: Update network strings table
        }

        public virtual void SendClientMessages( bool sendSnapshots ) {
            foreach (var client in Clients) {
                if ( !client.ShouldSendMessage() ) {
                    continue;
                }

                if (client.NetChannel != null) {
                    client.NetChannel.Transmit();
                    client.UpdateSendState();
                } else {
                    Out.Debug("Client has no NetChannel!");
                }
            }
        }

        public virtual bool IsActive() {
            return State >= EServerState.Active;
        }

        public virtual bool IsLoading() {
            return State == EServerState.Loading;
        }

        public virtual bool IsPaused() {
            return State == EServerState.Paused;
        }

        public virtual void Shutdown() {
            if ( !IsActive() ) {
                return;
            }

            State = EServerState.Dead;
            foreach (var client in Clients) {
                if (client.IsConnected()) {
                    client.Disconnect("Server shutting down");
                } else {
                    client.Clear();
                }

                Clients.Remove(client);
            }

            Thread.Sleep(100);
            Clear();
        }

        public virtual void Clear() {
            // TODO: Remove & clear stringtables
            State = EServerState.Dead;
            TickCount = 0;

            _serverQueryChallenges.Clear();
        }

        public void WriteDeltaEntities(BaseClient client, ClientFrame to, ClientFrame from, BufferWrite buf ) {
            var u = new EntityWriteInfo();
            u.Buf = buf;
            u.To = to;
            u.ToSnapshot = to.GetSnapshot();
            u.Baseline = client.Baseline;
            u.FullProps = 0;
            u.Server = this;
            u.ClientEntity = client.EntityIndex;
            u.CullProps = true;

            if (from != null) {
                u.AsDelta = true;
                u.From = from;
                u.FromSnapshot = from.GetSnapshot();
            } else {
                u.AsDelta = false;
                u.From = null;
                u.FromSnapshot = null;
            }

            u.HeaderCount = 0;

            // set from_baseline pointer if this snapshot may become a baseline update
            if ( client.BaselineUpdateTick == -1 ) {
                // TODO: Clear client baselines sent
                // TODO: Set 'to' from baseline to client.BaselinesSent
            }

            u.Buf.WriteUShort( (ushort)ENetCommand.SvcPacketEntities );
            u.Buf.WriteInt( u.ToSnapshot.NumEntities );

            if (u.AsDelta) {
                u.Buf.WriteByte(1);
                u.Buf.WriteInt(u.From.TickCount);
            } else {
                u.Buf.WriteByte(0);
            }

            u.Buf.WriteInt( client.BaselineUsed );

            // Store off current position 
            var savePos = u.Buf.Position;
        }

        public void WriteTempEntities(BaseClient baseClient, FrameSnapshot getSnapshot, FrameSnapshot lastSnapshot, BufferWrite msg, int maxTempEnts) {
            // CBaseServer::WriteTempEntities
        }
    }

    public class NetChallenge
    {
        public EndPoint Addr;
        public int Challenge;
        public float Time;
    }
}
