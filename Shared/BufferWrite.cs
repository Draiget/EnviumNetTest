using System.IO;
using System.Text;

namespace Shared
{
    public class BufferWrite
    {
        private MemoryStream _ms;
        private BinaryWriter _writer;

        public BufferWrite(byte[] data, int len) {
            _ms = new MemoryStream(data, 0, len);
            _writer = new BinaryWriter(_ms);
        }

        public BufferWrite() {
            _ms = new MemoryStream();
            _writer = new BinaryWriter(_ms);
        }

        public byte[] GetData() {
            return _ms.ToArray();
        }

        public void Reset() {
            if( _ms != null ) {
                _ms = new MemoryStream();
            }

            if( _writer != null ) {
                _writer = new BinaryWriter(_ms);
            }
        }

        public void WriteUShort(ushort value) {
            _writer.Write(value);
        }

        public void WriteUInt(uint value) {
            _writer.Write(value);
        }

        public void WriteULong(ulong value) {
            _writer.Write(value);
        }

        public void WriteShort(short value) {
            _writer.Write(value);
        }

        public void WriteInt(int value) {
            _writer.Write(value);
        }

        public void WriteLong(long value) {
            _writer.Write(value);
        }

        public void WriteChar(char value) {
            _writer.Write(value);
        }

        public void WriteBool(bool value) {
            _writer.Write(value);
        }

        public void WriteByte(byte value) {
            _writer.Write(value);
        }

        public bool WriteBytes(byte[] value) {
            _writer.Write(value);
            return true;
        }

        public void WriteString(string value) {
            var temp = Encoding.UTF8.GetBytes(value);
            _writer.Write(temp);
            _writer.Write('\0');
        }

        public void WriteFloat(float value) {
            _writer.Write(value);
        }

        public void WriteDouble(double value) {
            _writer.Write(value);
        }

        public int GetNumBitsWritten() {
            return _ms.ToArray().Length;
        }
    }
}