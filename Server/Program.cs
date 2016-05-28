using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Shared;
using Shared.Messages;

namespace Server
{
    class Program
    {
        public static bool SvCheats = false;
        public static bool SvLan = false;
        public static string SvPassword = "";
        public static readonly int SvMaxPlayers = 32;

        public static readonly float Tickrate = 33;

        public static readonly float MinFrametime = 0.001f;
        public static readonly float MaxFrametime = 0.1f;

        private static IPEndPoint _boundEndPoint;
        private static Socket _socket;
        private static byte[] _buffer;
        private static BaseServer _serverHandler;

        public static int TickCount;
        public static long LastTickElapse;

        private static long _tickTempTime;
        private static long _lastTickStart;
        private static long _lastTick;
        
        public static float Realtime;
        public static float HostFrametime;
        public static float HostFrametimeUnbounded = 0.0f;
        public static float HostFrametimeStdDeviation = 0.0f;

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

            _serverHandler = new BaseServer(_socket);
            Networking.Initialize();

            new Thread(GameTick) { IsBackground = true }.Start();
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

        public static float ACurTime() {
            return (float)(TickCount * 0.015);
        }

        private static void GameTick() {
            while( true ) {
                _tickTempTime = DateTime.Now.Ticks;
                if( _tickTempTime - _lastTick < Math.Floor(9899999f / Tickrate) - LastTickElapse )
                    continue;

                _lastTickStart = _tickTempTime;

                try {
                    GameFrame(Utils.SysTime());
                } catch( Exception ) {
                    ;
                }

                _lastTick = DateTime.Now.Ticks;
                LastTickElapse = _lastTick - _lastTickStart;
                TickCount++;
            }
        }

        private static void GameFrame(double time) {
            AccumulateTime((float)time);
            Networking.SetTime(time);
            _serverHandler.RunFrame();
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

        
    }

    
}
