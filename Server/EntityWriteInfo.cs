using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.Buffers;

namespace Server
{
    public class EntityWriteInfo : EntityInfo
    {
        public BufferWrite Buf;
        public int ClientEntity;

        public PackedEntity OldPack;
        public PackedEntity NewPack;

        public FrameSnapshot FromSnapshot;
        public FrameSnapshot ToSnapshot;

        public FrameSnapshot Baseline;

        public BaseServer Server;

        public int FullProps;
        public bool CullProps;
    }
}
