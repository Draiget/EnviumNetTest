﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared;

// u811@r64.nalog.ru
namespace Server
{
    public class BaseServer
    {
        private const int MaxChallenges = 16384;
        private const float ChallengeLifetime = 60 * 60.0f;

        private readonly Socket _socket;
        private List<NetChallenge> _serverQueryChallenges;
        private List<BaseClient> _clients;

        public BufferWrite Signon;

        public BaseServer(Socket serverSocket) {
            _socket = serverSocket;
            _serverQueryChallenges = new List<NetChallenge>();
            _clients = new List<BaseClient>();
            Signon = new BufferWrite();
        }

        public void ProcessConnectionlessPacket(NetPacket packet) {
            var msg = packet.Message;

            var type = (EConnectionType)msg.ReadByte();
            // TODO: Check connectionless rate limit for remote address

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
                    Console.WriteLine("Unknown connectionless type '{0}'!", type);
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

            if( _clients.Count + 1 > Program.SvMaxPlayers ) {
                RejectConnecton(addr, "Server is full.\n");
                return;
            }

            var client = new BaseClient(this);
            var channel = Networking.CreateChannel(_socket, name, addr, client);
            if( channel == null ) {
                RejectConnecton(addr, "Failed to create net channel!\n");
                return;
            }

            // set channel challenge
            channel.SetChallengeNr(challenge);

            // make sure client is reset and clear
            client.Connect(name, channel);

            // add client to global list
            _clients.Add(client);

            // tell client connection worked, now use netchannels
            Networking.OutOfBandPrintf(_socket, addr, "{0}00000000000000", (char)EConnectionType.ConnectionAccept);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Client \"{0}\" has connected from [{1}]", client.GetClientName(), channel.GetRemoteAddress());
        }

        private void ReplyChallenge(EndPoint clientEp) {
            var msg = new BufferWrite();

            // get a free challenge number
            var challengeNr = GetChallengeNr( clientEp );
            
            msg.WriteBytes(NetProtocol.ConnectionlessHeader);

            msg.WriteByte((byte)EConnectionType.ServerChallenge);
            msg.WriteInt(challengeNr);
            msg.WriteInt(1); // auth protocol
            msg.WriteString("EE00");

            Networking.SendPacket(null, _socket, clientEp, msg.GetData());
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
                        Console.WriteLine("Old challenge from {0}", addr);
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
                Console.WriteLine("No challenge from {0}", addr);
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

        private bool CheckIPConnectionReuse(EndPoint addr) {
            var sameConnections = _clients.Count(client => client.IsConnected() && !client.IsActive() && client.NetChannel.GetRemoteAddress().CompareAddr(addr, true));
            if( sameConnections > Networking.MaxReusePerIp ) {
                Console.WriteLine("Too many connect packets from {0}!", addr.ToString(true));
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
            Networking.OutOfBandPrintf(_socket, addr, "{0}{1}", (char)EConnectionType.ConnectionReject, message);
        }

        public void SendPendingServerInfo() {
            foreach (var client in _clients) {
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
            foreach (var client in _clients) {
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
            foreach (var client in _clients) {
                if ( client.HasConvarsChanged() ) {
                    client.UpdateUserSettings();
                }
            }
        }

        public void BroadcastMessage(INetMessage msg, bool onlyActive = false, bool reliable = false) {
            foreach (var client in _clients) {
                if ( (onlyActive && !client.IsActive()) || !client.IsSpawned() ) {
                    continue;
                }

                if ( !client.NetChannel.SendNetMsg( msg, reliable ) ) {
                    if ( msg.IsReliable() || reliable ) {
                        Console.WriteLine("BaseServer.BroadcastMessage: Reliable broadcast message overflow for client {0}", client.GetClientName());
                    }
                }
            }
        }

        public void BroadcastMessage( INetMessage msg, IRecipientFilter filter ) {
            if( filter.IsInitMessage() ) {
                if ( !msg.WriteToBuffer( Signon ) ) {
                    Console.WriteLine("BaseServer.BroadcastMessage: Init message would overflow signon buffer!");
                }

                return;
            }

            msg.SetReliable( filter.IsReliable() );

            foreach (var client in _clients) {
                if ( !client.IsSpawned() ) {
                    continue;
                }

                if ( !client.NetChannel.SendNetMsg(msg) ) {
                    if ( msg.IsReliable() ) {
                        Console.WriteLine("BaseServer.BroadcastMessage: Reliable filter message overflow for client {0}", client.GetClientName());
                    }
                }
            }
        }

        public int GetTick() {
            return Program.TickCount;
        }

        public void UserInfoChanged(BaseClient client) {
            // TODO: Update network strings table
        }
    }

    public class NetChallenge
    {
        public EndPoint Addr;
        public int Challenge;
        public float Time;
    }
}