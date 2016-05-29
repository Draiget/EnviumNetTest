namespace Shared.Messages
{
    public interface IConnectionlessPacketHandler
    {
        bool ProcessConnectionlessPacket( NetPacket packet );
    }
}
