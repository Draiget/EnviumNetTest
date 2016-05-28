using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.Buffers;
using Shared.Enums;
using Shared.Messages;

namespace Shared.NetMessages
{
    public class NetMessageTick : NetMessage
    {
        public int Tick;
        public float HostFrameTime;
        public float HostFrameTimeStdDeviation;

        public NetMessageTick() {
            Reliable = false;
            HostFrameTime = 0;
            HostFrameTimeStdDeviation = 0;
        }

        public NetMessageTick( int tick, float hostFrametime, float hostFrametimeStdDeviation) {
            Reliable = false;
            Tick = tick;
            HostFrameTime = hostFrametime;
            HostFrameTimeStdDeviation = hostFrametimeStdDeviation;
        }

        public override bool Process() {
            return ChannelHandler.ProcessTick(this);
        }

        public override bool WriteToBuffer(BufferWrite buffer) {
            buffer.WriteInt(Tick);
            buffer.WriteULong(Utils.Clamp<ulong>((ulong)( HostFrameTime * Networking.NetTickScaleUp ), 0, 65535));
            buffer.WriteULong(Utils.Clamp<ulong>((ulong)( HostFrameTimeStdDeviation * Networking.NetTickScaleUp ), 0, 65535));

            return true;
        }

        public override bool ReadFromBuffer(BufferRead buffer) {
            Tick = buffer.ReadInt();
            HostFrameTime = buffer.ReadULong() / Networking.NetTickScaleUp;
            HostFrameTimeStdDeviation = buffer.ReadULong() / Networking.NetTickScaleUp;

            return true;
        }

        public override string GetName() {
            return GetMsgType().ToString();
        }

        public override ENetCommand GetMsgType() {
            return ENetCommand.Tick;
        }
    }
}
