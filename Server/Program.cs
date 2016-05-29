using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Plugins;
using Shared;
using Shared.Enums;
using Shared.Messages;

namespace Server
{
    class Program
    {
        public static bool SvCheats = false;
        public static bool SvLan = false;
        public static string SvPassword = "";
        public static readonly int SvMaxPlayers = 32;
        public static float SvTimeout = 200f;

        public static float Tickrate = 33;
        public static readonly float DefaultTickInterval = 0.015f;

        public static readonly float MinFrametime = 0.001f;
        public static readonly float MaxFrametime = 0.1f;

        private static IPEndPoint _boundEndPoint;
        private static Socket _socket;
        private static byte[] _buffer;
        private static GameServer _serverHandler;
        
        public static float Realtime;
        public static float HostFrametime;
        public static float HostFrametimeUnbounded = 0.0f;
        public static float HostFrametimeStdDeviation = 0.0f;

        public static ServerPlugin ServerPluginHandler;

        private static void Main(string[] args) {
            Console.Title = "ENVIUM DEDICATED SERVER";
            Console.WriteLine("Initializing the server ...");
            _buffer = new byte[ 4096 ];
            _boundEndPoint = new IPEndPoint(IPAddress.Any, 644);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try {
                _socket.Bind(_boundEndPoint);
            } catch (SocketException ex) {
                Console.WriteLine("Network: Cannot bound at address {0}: {1}", _boundEndPoint, ex.Message);
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Network: Socket bounded at {0}", _socket.LocalEndPoint as IPEndPoint);

            ServerPluginHandler = new ServerPlugin();
            _serverHandler = new GameServer(_socket) {
                State = EServerState.Loading, 
                TickInterval = GetTickInterval()
            };

            Networking.Initialize();

            new Thread(GameTick) { IsBackground = true }.Start();
            _serverHandler.State = EServerState.Active;
            Console.WriteLine("Done loading.");

            var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref clientEp, DoReceiveFrom, clientEp);

            string read;
            while( ( read = Console.ReadLine() ) != "exit" ) {
                if( read == "help") {
                    continue;
                }

                if ( read == "curtime" ) {
                    var time = Utils.SysTime();
                    Console.WriteLine("Current server time: {0}", time);
                    continue;
                }

                Console.WriteLine("Unknown command \"{0}\".", read);
            }
        }

        private static float GetTickInterval() {
            var tickInterval = DefaultTickInterval;
            var tickrate = Tickrate;
            if ( tickrate > 10 ) {
                tickInterval = 1.0f / tickrate;
            }

            return tickInterval;
        }

        private static void GameTick() {
            while( true ) {
                try {
                    HostRunFrame( (float)Utils.SysTime() );
                } catch( Exception ) {
                    ;
                }
            }
        }

        private static void AccumulateTime(float dt) {
            Realtime += dt;
            HostFrametime = dt;

            if (Networking.HostTimescale > 0f && SvCheats) {
                var fullscale = Networking.HostTimescale;

                HostFrametime *= fullscale;
                HostFrametimeUnbounded = HostFrametime;
            } else {
                HostFrametimeUnbounded = HostFrametime;
                HostFrametime = Utils.Min(HostFrametime, MaxFrametime);
                HostFrametime = Utils.Max(HostFrametime, MinFrametime);
            }
        }

        private static void DoReceiveFrom(IAsyncResult iar) {
            try {
                var packet = new NetPacket();
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                var dataLen = 0;
                byte[] data = null;
                try {
                    dataLen = _socket.EndReceiveFrom(iar, ref clientEp);
                    packet.Assign(clientEp, dataLen, _buffer);
                } catch( Exception e ) {
                    Console.WriteLine("Recv error ({0}): {1}", e.GetType(), e.Message);
                } finally {
                    var newEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                    _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref newEp, DoReceiveFrom, newEp);
                }

                // do not need process empty packets
                if( dataLen == 0 && packet.HasData ) {
                    return;
                }

                // check for connectionless header (0xffffffff) first
                if( Networking.IsConnectionlessHeader(packet.Data) ) {
                    packet.Message.ReadInt(); // read connectionless header (the -1)

                    _serverHandler.ProcessConnectionlessPacket(packet);
                    return;
                }

                var channel = Networking.FindNetChannelFor(_socket, packet.From);
                if( channel != null ) {
                    channel.ProcessPacket(packet, true);
                }
            } catch( ObjectDisposedException e ) {
                Console.WriteLine("Recv dispose error ({0}): {1}", e.GetType(), e.Message);
            }
        }

        private static float _hostRemainder;
        private static int _hostFrameTicks;
        private static int _hostCurrentFrameTick;

        public static void HostRunFrame(float time) {
            AccumulateTime(time);

            var prevRemainder = _hostRemainder;
            if ( prevRemainder < 0 ) {
                prevRemainder = 0;
            }

            _hostRemainder += HostFrametime;

            var numticks = 0;
            if( _hostRemainder >= _serverHandler.TickInterval ) {
                numticks = (int)( _hostRemainder / _serverHandler.TickInterval );
                _hostRemainder -= numticks * _serverHandler.TickInterval;
            }

            _hostFrameTicks = numticks;
            _hostCurrentFrameTick = 0;

            for( var tick = 0; tick < numticks; tick++ ) {
                Networking.RunFrame(time);

                ++_hostCurrentFrameTick;

                var finalTick = tick == (numticks - 1);
                HostRunFrame_Server(finalTick);

                // TODO: Send queued network packets
            }

        }

        private static void HostRunFrame_Server(bool finalTick) {
            _serverHandler.RunFrame();
            _serverHandler.TickCount++;

            if( finalTick ) {
                SendClientUpdates();
            }
        }

        public static void SendClientUpdates() {
            _serverHandler.SendClientMessages();
        }
    }

    
}
