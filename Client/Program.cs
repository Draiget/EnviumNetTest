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
        public static bool SvCheats = false;
        public static readonly float ClResend = 1.4f;
        public static readonly float ClRate = 30000.0f;
        public static readonly bool ClAllowUpload = false;

        public static readonly int ProtocolVersion = 12;
        public static readonly float Tickrate = 33;
        public static readonly float DefaultTickInterval = 0.015f;

        private static Socket _socket;

        public static string ClientName;
        public static string ServerPassword;

        private static ClientState _clientState;

        public static readonly float MinFrametime = 0.001f;
        public static readonly float MaxFrametime = 0.1f;

        public static float Realtime;
        public static float HostFrametime;
        public static float HostFrametimeUnbounded = 0.0f;
        public static float HostFrametimeStdDeviation = 0.0f;

        public static float IntervalPerTick = 0.03f;

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
            IntervalPerTick = GetTickInterval();
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

        private static float GetTickInterval() {
            var tickInterval = DefaultTickInterval;
            var tickrate = Tickrate;
            if( tickrate > 10 ) {
                tickInterval = 1.0f / tickrate;
            }

            return tickInterval;
        }

        private static void GameTick() {
            while (true) {
                try {
                    HostRunFrame((float)Utils.SysTime());
                } catch (Exception) {
                    ;
                }
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

        private static void AccumulateTime(float dt) {
            Realtime += dt;
            HostFrametime = dt;

            if( Networking.HostTimescale > 0f && SvCheats ) {
                var fullscale = Networking.HostTimescale;

                HostFrametime *= fullscale;
                HostFrametimeUnbounded = HostFrametime;
            } else {
                HostFrametimeUnbounded = HostFrametime;
                HostFrametime = Utils.Min(HostFrametime, MaxFrametime);
                HostFrametime = Utils.Max(HostFrametime, MinFrametime);
            }
        }

        private static float _hostRemainder;
        private static int _hostFrameTicks;
        private static int _hostCurrentFrameTick;

        private static void HostRunFrame(float time) {
            AccumulateTime(time);

            var prevRemainder = _hostRemainder;
            if( prevRemainder < 0 ) {
                prevRemainder = 0;
            }

            _hostRemainder += HostFrametime;

            var numticks = 0;
            if( _hostRemainder >= IntervalPerTick ) {
                numticks = (int)( _hostRemainder / IntervalPerTick );
                _hostRemainder -= numticks * IntervalPerTick;
            }

            _hostFrameTicks = numticks;
            _hostCurrentFrameTick = 0;

            _clientState.FrameTime = HostFrametime;
            for( var tick = 0; tick < numticks; tick++ ) {
                Networking.RunFrame(time);

                ++_hostCurrentFrameTick;

                var finalTick = tick == ( numticks - 1 );

                HostRunFrame_Input(prevRemainder, finalTick);
                prevRemainder = 0;

                HostRunFrame_Client(finalTick);

                // TODO: Send queued network packets
            }
        }

        private static void HostRunFrame_Input(float accumulatedExtraSamples, bool finalTick) {
            // TODO: CL_Move
        }

        public static void HostRunFrame_Client(bool frameFinished) {
            if( (_clientState.NetChannel != null && _clientState.NetChannel.IsTimedOut()) && frameFinished && _clientState.IsConnected() ) {
                Console.WriteLine("Server connection timed out.");
                // TODO: Show dialog

                _clientState.Disconnect(true);
                return;
            }

            if( _clientState.NetChannel != null && _clientState.NetChannel.IsTimingOut() ) {
                Console.Title = string.Format("Timing out: {0:####.##}", _clientState.NetChannel.GetTimeoutSeconds() - _clientState.NetChannel.GetTimeSinceLastReceived());
            }

            _clientState.RunFrame();
        }
    }
}
