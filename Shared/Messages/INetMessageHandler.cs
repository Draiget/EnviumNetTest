using Shared.NetMessages;

namespace Shared.Messages
{
    public interface INetMessageHandler
    {
        bool ProcessTick( NetMessageTick msg );
        bool ProcessSignonState( NetMessageSignonState msg );
    }
}