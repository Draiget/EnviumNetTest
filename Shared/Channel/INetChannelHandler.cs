namespace Shared.Channel
{
    public interface INetChannelHandler
    {
        void ConnectionStart(NetChannel netChannel);
        void ConnectionClosing(string reason);

        void FileReceived(string fileName, uint transferId);
        void FileDenied(string fileName, uint transfterId);
        void FileRequested(string fileName, uint transferId);

        void PacketStart(int inSequence, int outAcknowledged);
        void PacketEnd();
    }
}
