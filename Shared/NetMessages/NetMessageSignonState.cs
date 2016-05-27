using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Shared.NetMessages
{
    public class NetMessageSignonState : NetMessage
    {
        private ESignonState _state;

        public NetMessageSignonState() { }

        public NetMessageSignonState(ESignonState state) {
            _state = state;
        }

        public override bool Process() {
            return ChannelHandler.ProcessSignonState(this);
        }

        public override bool WriteToBuffer(BufferWrite buffer) {
            buffer.WriteByte((byte)_state);

            return true;
        }

        public override bool ReadFromBuffer(BufferRead buffer) {
            _state = (ESignonState)buffer.ReadByte();

            return true;
        }

        public override string GetName() {
            return GetMsgType().ToString();
        }

        public override ENetCommand GetMsgType() {
            return ENetCommand.SignonState;
        }

        public ESignonState SignonState {
            get { return _state; }
        }
    }
}
