﻿using Shared.Buffers;
using Shared.Channel;
using Shared.Enums;

namespace Shared.Messages
{
    public abstract class NetMessage : INetMessage
    {
        protected NetMessage() {
            Reliable = true;
            _netChannel = null;
        }

        public INetMessageHandler ChannelHandler;
        private NetChannel _netChannel;
        protected bool Reliable;

        public void SetNetChannel(NetChannel channel) {
            _netChannel = channel;
        }

        public bool IsReliable() {
            return Reliable;
        }

        public void SetReliable(bool state) {
            Reliable = state;
        }

        public abstract bool Process();
        public abstract bool WriteToBuffer(BufferWrite buffer);
        public abstract bool ReadFromBuffer(BufferRead buffer);
        public abstract string GetName();
        public abstract ENetCommand GetMsgType();
    }
}
