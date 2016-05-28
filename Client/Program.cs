using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Shared;
using Shared.Messages;

namespace Client
{
    class Program
    {
        public static readonly float ClResend = 1.4f;
        public static readonly float ClRate = 30000.0f;
        public static readonly bool ClAllowUpload = false;

        public static readonly int ProtocolVersion = 12;
        public static readonly float Tickrate = 33;

        private static Socket _socket;

        public static string ClientName;
        public static string ServerPassword;

        private static ClientState _clientState;

        public static long TicksElapsed;
        public static long LastTickElapse;

        private static long _tickTempTime;
        private static long _lastTickStart;
        private static long _lastTick;

        private static void Main(string[] args) {
            Console.Title = "Envium client";
            ClientName = "Draiget";
            ServerPassword = "";
            _clientState = new ClientState();

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            Networking.Initialize();
            _clientState.Initialize(_socket);
            new Thread(NetTick) { IsBackground = true }.Start();
            new Thread(GameTick) { IsBackground = true }.Start();

            Console.WriteLine(">> Ready for commands.");

            var read = string.Empty;
            while ( (read = Console.ReadLine()) != "exit" ) {
                if( read != null && read.StartsWith("connect") ) {
                    _clientState.Connect(read.Substring(8));
                    continue;
                }

                Console.WriteLine("Unknown command \"{0}\".", read);
            }
        }

        private static void GameTick() {
            while (true) {
                _tickTempTime = DateTime.Now.Ticks;
                if( _tickTempTime - _lastTick < Math.Floor(9899999f / Tickrate) - LastTickElapse )
                    continue;

                _lastTickStart = _tickTempTime;

                try {
                    GameFrame(Utils.SysTime());
                } catch (Exception) {
                    ;
                }

                _lastTick = DateTime.Now.Ticks;
                LastTickElapse = _lastTick - _lastTickStart;
                TicksElapsed++;
            }
        }

        private static void GameFrame(double time) {
            Networking.SetTime(time);
            _clientState.SetFrameTime((float)time);

            if ( _clientState != null ) {
                _clientState.RunFrame( time );
            }
        }

        private static void NetTick() {
            var netbuffer = new byte[ 16936 ];
            var serverEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

            while (true) {
                try {
                    var recvLen = 0;
                    try {
                        recvLen = _socket.ReceiveFrom(netbuffer, netbuffer.Length, SocketFlags.None, ref serverEp);
                    } catch (SocketException e) {
                        Console.WriteLine("Receive error ({0}): {1}", e.GetType(), e.Message);
                        continue;
                    }

                    if (recvLen <= 0) {
                        continue;
                    }

                    var packet = new NetPacket();
                    packet.Assign(serverEp, recvLen, netbuffer);

                    if( Networking.IsConnectionlessHeader(packet.Data) ) {
                        packet.Message.ReadInt(); // read connectionless header (the -1)

                        _clientState.ProcessConnectionlessPacket(packet);
                        continue;
                    }

                    if( _clientState.NetChannel != null ) {
                        _clientState.NetChannel.ProcessPacket(packet, true);
                    }
                } catch (Exception e) {
                    Console.WriteLine("Unknown network think error ({0}): {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
                }
            }
        }

    }
}
