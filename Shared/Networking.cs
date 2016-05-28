using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared.Buffers;
using Shared.Channel;

namespace Shared
{
    public class Networking
    {
        public static readonly int ConnectionRetries = 4;

        private static List<NetChannel> _netChannels;
        public static readonly int MaxReusePerIp = 5;
        public static readonly float ConnectionProblemTime = 4.0f;
        public static readonly float SignonTimeout = 300.0f;

        public static readonly int DefaultRate = 1000;
        public static readonly int MinRate = 1000;
        public static readonly int MaxRate = 1024 * 1024;

        public static readonly float NetTickScaleUp = 100000.0f;

        public static float NetMaxcleartime = 4.0f;
        public static float HostTimescale = 1f;

        /// <summary>
        /// Current time, updated each frame
        /// </summary>
        public static double NetTime {
            get;
            private set;
        }

        private static double _lastRealtime;

        public static void Initialize() {
            _netChannels = new List<NetChannel>();
        }

        public static NetChannel CreateChannel(Socket sock, string clientName, EndPoint clientEp, INetChannelHandler handler, bool forceNewChannel = false) {
            NetChannel channel = null;

            if (!forceNewChannel && clientEp != null) {
                if ((channel = Networking.FindNetChannelFor(sock, clientEp)) != null) {
                    channel.Clear();
                }
            }

            if (channel == null) {
                channel = new NetChannel();
            }

            channel.Setup(sock, clientName, clientEp, handler);
            return channel;
        }

        public static void RemoveChannel(NetChannel channel) {
            if (channel == null) {
                return;
            }

            for (var i = 0; i < _netChannels.Count; i++) {
                if (_netChannels[i] == channel) {
                    _netChannels.RemoveAt(i);
                }
            }

            // TODO: Clear send queue
        }

        public static NetChannel FindNetChannelFor(Socket serverSocket, EndPoint clientEp) {
            foreach (var channel in _netChannels) {
                if (channel.GetSocket() != serverSocket) {
                    continue;
                }

                if (channel.GetRemoteAddress() == clientEp) {
                    return channel;
                }
            }

            return null;
        }

        public static bool IsConnectionlessHeader(byte[] data) {
            for (var i = 0; i < NetProtocol.ConnectionlessHeader.Length; i++) {
                if( data[ i ] != NetProtocol.ConnectionlessHeader[ i ] ) {
                    return false;
                }
            }

            return true;
        }

        public static int SendPacket(NetChannel channel, Socket socket, EndPoint to, byte[] data) {
            var len = 0;
            try {
                len = socket.SendTo(data, SocketFlags.None, to);
            } catch (SocketException e) {
                if (e.SocketErrorCode == SocketError.WouldBlock) {
                    return 0;
                }

                if (e.SocketErrorCode == SocketError.ConnectionReset) {
                    return 0;
                }

                if (e.SocketErrorCode == SocketError.AddressNotAvailable) {
                    return 0;
                }
            }

            return len;
        }

        public static void OutOfBandPrintf(Socket socket, EndPoint addr, string format, params object[] args) {
            var msg = new BufferWrite();

            msg.WriteBytes(NetProtocol.ConnectionlessHeader);
            msg.WriteString(string.Format(format, args));

            SendPacket(null, socket, addr, msg.GetData());
        }

        public static bool StringToAddr(string address, ref EndPoint ep) {
            IPAddress ip;
            if( address.Contains(":") ) {
                var temp = address.Split(':');
                var port = 0;

                try {
                    ip = IPAddress.Parse(temp[0]);
                } catch( Exception ) {
                    return false;
                }

                try {
                    port = Int32.Parse(temp[1]);
                } catch( Exception ) {
                    return false;
                }

                ep = new IPEndPoint(ip, port);
                return true;
            }

            try {
                ip = IPAddress.Parse(address);
            } catch (Exception) {
                return false;
            }

            ep = new IPEndPoint(ip, 0);
            return true;
        }

        public static void SetTime(double realtime) {
            var frametime = realtime - _lastRealtime;
            _lastRealtime = realtime;

            if (frametime > 1.0f) {
                frametime = 1.0f;
            } else if(frametime < 0.0f) {
                frametime = 0.0f;
            }

            // adjust network time so fakelag works with host_timescale
            NetTime += frametime * HostTimescale;
        }
    }
}