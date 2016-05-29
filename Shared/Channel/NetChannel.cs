using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared.Buffers;
using Shared.Enums;
using Shared.Messages;

namespace Shared.Channel
{
    public class NetChannel
    {
        private Socket _socket;
        private EndPoint _remoteAddress;

        private INetChannelHandler _messageHandler;
        private List<NetMessage> _netMesages;

        public BufferWrite StreamReliable;
        public BufferWrite StreamUnreliable;

        private int _inSequenceNr;
        private int _outSequenceNrAck;
        private int _outSequenceNr;
        private int _chokedPackets;
        private int _packetDrop;
        private byte _inReliableState;
        private byte _outReliableState;

        private int _challengeNr;

        private int _rate;
        private double _clearTime;
        private float _timeout;
        private float _connectTime;
        private float _lastReceived;

        private float _remoteFrameTime;
        private float _remoteFrameTimeStdDeviation;

        private string _clientName;

        public NetChannel() {
            _netMesages = new List<NetMessage>();

            StreamReliable = new BufferWrite();
            StreamUnreliable = new BufferWrite();

            _timeout = Networking.SignonTimeout;
            _rate = Networking.DefaultRate;

            _inReliableState = 0;
            _outReliableState = 0;
        }

        public void Setup(Socket socket, string clientName, EndPoint clientEp, INetChannelHandler handler) {
            _socket = socket;
            _remoteAddress = clientEp;

            _lastReceived = (float)Networking.NetTime;
            _connectTime = (float)Networking.NetTime;

            _clientName = clientName;
            _messageHandler = handler;

            _timeout = Networking.SignonTimeout;
            _rate = Networking.DefaultRate;

            // Prevent the first message from getting dropped after connection is set up.
            _outSequenceNrAck = 1;
            _inSequenceNr = 0;
            _outSequenceNrAck = 0;
            _inReliableState = 0; // last remote reliable state
            _outReliableState = 0; // our current reliable state
            _chokedPackets = 0;

            _challengeNr = 0;
            _chokedPackets = 0;

            handler.ConnectionStart(this);
        }

        public void SetDataRate(float rate) {
            _rate = (int)(rate > Networking.MaxRate ? Networking.MaxRate : ( rate < Networking.MinRate ? Networking.MinRate : rate));
        }

        public int GetDataRate() {
            return _rate;
        }

        public void Clear() {
            // TODO: Reset wait list

            Reset();
        }

        public void Reset() {
            StreamReliable.Reset();
            StreamUnreliable.Reset();
            _clearTime = 0.0f;
            _chokedPackets = 0;
        }

        public int ProcessPacketHeader(NetPacket packet) {
            var sequence = packet.Message.ReadInt();
            var sequenceAck = packet.Message.ReadInt();
            var flags = packet.Message.ReadByte();

            // TODO: Check packet cheksumm
            // TODO: Check choked packets

            var choked = 0;

            if( sequence <= _inSequenceNr ) {
                return -1;
            }

            // dropped packets don't keep the message from being used
            _packetDrop = sequence - ( _inSequenceNr + choked + 1 );

            if( _packetDrop > 0 ) {
                // TODO: Debug dropping
            }

            _inSequenceNr = sequence;
            _outSequenceNrAck = sequenceAck;

            return flags;
        }

        public void ProcessPacket(NetPacket packet, bool hasHeader) {
            var msg = packet.Message;
            var flags = 0;

            if( hasHeader ) {
                flags = ProcessPacketHeader(packet);
            }

            // check for invalid packet or header
            if( flags == -1 ) {
                return;
            }

            _lastReceived = (float)Networking.NetTime;
            _messageHandler.PacketStart( _inSequenceNr, _outSequenceNrAck );
            Console.WriteLine("Packet read!");

            if( msg.GetNumBitsLeft() > 0 ) {
                if ( !ProcessMessages(msg) ) {
                    return;
                }
            }

            _messageHandler.PacketEnd();
        }

        public bool ProcessMessages(BufferRead buf) {
            while (true) {
                if ( buf.GetNumBitsLeft() <= 0 ) {
                    break;
                }

                var cmd = buf.ReadUShort();
                if( cmd <= (ushort)ENetCommand.NetFile ) {
                    if( !ProcessControlMessage((ENetCommand)cmd, buf) ) {
                        return false;
                    }

                    continue;
                }

                var netMsg = FindMessage(cmd);
                if (netMsg != null) {
                    var msgName = netMsg.GetName();
                    if (!netMsg.ReadFromBuffer(buf)) {
                        Console.WriteLine("NetChannel: Failed reading message {0} from {1}", msgName, _remoteAddress);
                        return false;
                    }

                    var ret = netMsg.Process();
                    if (!ret) {
                        Console.WriteLine("NetChannel: Failed processing message {0}.", msgName);
                        return false;
                    }
                } else {
                    Console.WriteLine("NetChannel: Unknown net message ({0}) from {1}", cmd, _remoteAddress);
                    return false;
                }
            }

            return true;
        }

        private bool ProcessControlMessage( ENetCommand cmd, BufferRead buf ) {
            if ( cmd == ENetCommand.NetNop ) {
                return true;
            }

            if ( cmd == ENetCommand.NetDisconnect ) {
                var reason = buf.ReadString();
                _messageHandler.ConnectionClosing( reason );
                return false;
            }

            if ( cmd == ENetCommand.NetFile ) {
                var transferId = buf.ReadUInt();
                var fileName = buf.ReadString();

                if (buf.ReadByte() != 0 && IsSafeFileToDownload(fileName)) {
                    _messageHandler.FileRequested( fileName, transferId );
                } else {
                    _messageHandler.FileDenied( fileName, transferId );
                }

                return true;
            }

            Console.WriteLine("NetChannel: Received bad control command type {0} from {1}.", (ushort)cmd, _remoteAddress);
            return false;
        }

        public NetMessage FindMessage(ushort type) {
            return _netMesages.FirstOrDefault(message => message.GetMsgType() == (ENetCommand)type);
        }

        public bool RegisterMessage(NetMessage msg) {
            if( FindMessage((ushort)msg.GetMsgType()) != null ) {
                return false;
            }

            _netMesages.Add(msg);
            msg.SetNetChannel(this);

            return true;
        }

        public Socket GetSocket() {
            return _socket;
        }

        public EndPoint GetRemoteAddress() {
            return _remoteAddress;
        }

        public void SetChallengeNr(int challenge) {
            _challengeNr = challenge;
        }

        public void Shutdown(string reason = null) {
            if( _socket == null ) {
                return;
            }

            Clear();

            if( reason != null ) {
                StreamUnreliable.WriteUShort((ushort)ENetCommand.NetDisconnect);
                StreamUnreliable.WriteString(reason);
                Transmit();
            }

            _socket = null;
            _remoteAddress = null;

            if( _messageHandler != null ) {
                _messageHandler.ConnectionClosing(reason);
                _messageHandler = null;
            }

            _netMesages.Clear();
            Networking.RemoveChannel(this);
        }

        public void SetTimeout( float seconds ) {
            _timeout = seconds;

            // clamp timeout to one hour
            if( _timeout  > 3600.0f ) {
                _timeout = 3600.0f;
            } else if( _timeout <= Networking.ConnectionProblemTime ) {
                _timeout = Networking.ConnectionProblemTime;
            }
        }

        public int SendDatagram(BufferWrite datagram) {
            var send = new BufferWrite();

            send.WriteInt(_outSequenceNr);
            send.WriteInt(_inSequenceNr);

            send.WriteByte(_inReliableState);

            if( datagram != null ) {
                if( datagram.GetNumBitsWritten() > 0 ) {
                    send.WriteBytes(datagram.GetData());
                }
            }

            if( StreamUnreliable.GetNumBitsWritten() > 0 ) {
                send.WriteBytes(StreamUnreliable.GetData());
            }

            // clear unreliable data buffer
            StreamUnreliable.Reset();

            var bytesSend = Networking.SendPacket(this, _socket, _remoteAddress, send.GetData());
            // TODO: Calc sended size with udp header, and retreive stats
            // TODO: Network throttling

            if( _clearTime < Networking.NetTime ) {
                _clearTime = Networking.NetTime;
            }

            var addTime = bytesSend / _rate;
            _clearTime += addTime;

            if( Networking.NetMaxcleartime > 0.0f ) {
                var latestClearTime = Networking.NetTime + Networking.NetMaxcleartime;
                if ( _clearTime > latestClearTime ) {
                    _clearTime = latestClearTime;
                }
            }

            _chokedPackets = 0;
            _outSequenceNr++;

            // return send sequence nr
            return _outSequenceNr - 1;
        }

        public bool Transmit(bool onlyReliable = false) {
            // TODO: onlyReliable -> reset unreliable buffer stream
            if ( onlyReliable ) {
                StreamUnreliable.Reset();
            }

            return SendDatagram(null) != 0;
        }

        public bool SendData(BufferWrite msg, bool reliable = false) {
            if ( _remoteAddress == null ) {
                return true;
            }

            var buf = reliable ? StreamReliable : StreamUnreliable;
            return buf.WriteBytes(msg.GetData());
        }

        public bool SendNetMsg(INetMessage msg, bool forceReliable = false) {
            if ( _remoteAddress == null ) {
                return true;
            }

            var stream = StreamUnreliable;

            if ( msg.IsReliable() || forceReliable ) {
                stream = StreamReliable;
            }

            return msg.WriteToBuffer( stream );
        }

        public bool IsTimedOut() {
            return _lastReceived + _timeout < Networking.NetTime;
        }

        public bool IsTimingOut() {
            return _lastReceived + Networking.ConnectionProblemTime < Networking.NetTime;
        }

        public float GetTimeoutSeconds() {
            return _timeout;
        }

        public float GetTimeSinceLastReceived() {
            var time = (float)Networking.NetTime - _lastReceived;
            return time > 0.0f ? time : 0.0f;
        }

        public void SetRemoteFramerate( float frameTime, float frameTimeStdDeviation ) {
            _remoteFrameTime = frameTime;
            _remoteFrameTimeStdDeviation = frameTimeStdDeviation;
        }

        private static bool IsSafeFileToDownload( string filename ) {
            if( filename.Contains(":") || filename.Contains("..") ) {
                return false;
            }

            if( filename.Contains(".exe")
                || filename.Contains(".dll")
                || filename.Contains(".ini") ) 
            {
                return false;
            }

            return true;
        }

        public bool SendFile(string fileName, uint transferId) {
            if ( _remoteAddress == null ) {
                return true;
            }

            if( !CreateFragmentsFromFile(fileName, transferId ) ) {
                DenyFile( fileName, transferId );
                return false;
            }

            return true;
        }

        public void DenyFile(string fileName, uint transferId) {
            StreamReliable.WriteUShort( (ushort)ENetCommand.NetFile );
            StreamReliable.WriteUInt( transferId );
            StreamReliable.WriteString( fileName );
            StreamReliable.WriteByte( 0 );
        }

        private bool CreateFragmentsFromFile(string fileName, uint transferId) {
            // TODO: Check if file exists in file system
            // TODO: Check file total size with max limit 'NetMaxFileSize'
            // TODO: Create data fragments for file
            // TODO: Add framgents to file waiting list

            return false;
        }

        public bool HasPendingReliableData() {
            return StreamReliable.GetNumBitsWritten() > 0;
        }

        public bool CanPacket() {
            if( _remoteAddress != null && IPAddress.IsLoopback(( _remoteAddress as IPEndPoint ).Address) ) {
                return true;
            }

            // TODO: Has queued packets
            return _clearTime < Networking.NetTime;
        }

        public void SetChoked() {
            _outSequenceNr++;
            _chokedPackets++;
        }

        public int GetSequenceNr( EFlowType flow ) {
            if ( flow == EFlowType.Outgoing ) {
                return _outSequenceNr;
            }

            if ( flow == EFlowType.Incoming ) {
                return _inSequenceNr;
            }

            return 0;
        }
    }
}
