using System.IO;

namespace Shared.Buffers
{
    public class BufferRead
    {
        private MemoryStream _ms;
        private BinaryReader _reader;

        public BufferRead(byte[] data, int len) {
            _ms = new MemoryStream(data, 0, len);
            _reader = new BinaryReader(_ms);
        }

        public byte[] GetData() {
            return _ms.ToArray();
        }

        public void Reset() {
            if( _ms != null ) {
                _ms = new MemoryStream();
            }

            if ( _reader != null ) {
                _reader = new BinaryReader(_ms);
            }
        }

        public ushort ReadUShort() {
            return _reader.ReadUInt16();
        }

        public uint ReadUInt() {
            return _reader.ReadUInt32();
        }

        public ulong ReadULong() {
            return _reader.ReadUInt64();
        }

        public short ReadShort() {
            return _reader.ReadInt16();
        }

        public int ReadInt() {
            return _reader.ReadInt32();
        }

        public long ReadLong() {
            return _reader.ReadInt64();
        }

        public char ReadChar() {
            return _reader.ReadChar();
        }

        public bool ReadBool() {
            return _reader.ReadBoolean();
        }

        public byte ReadByte() {
            return _reader.ReadByte();
        }

        public byte[] ReadBytes(int count) {
            return _reader.ReadBytes(count);
        }

        public string ReadString() {
            string ret = string.Empty;
            while( GetNumBitsLeft() > 0 ) {
                char rd;
                if ( (rd = _reader.ReadChar()) == '\0' ) {
                    return ret;
                }

                ret += rd;
            }

            return ret;
        }

        public float ReadFloat() {
            return _reader.ReadSingle();
        }

        public double ReadDouble() {
            return _reader.ReadDouble();
        }

        public int GetNumBitsLeft() {
            return (int)(_reader.BaseStream.Length - _reader.BaseStream.Position);
        }
    }
}