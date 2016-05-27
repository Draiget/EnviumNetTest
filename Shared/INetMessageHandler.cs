using Shared.NetMessages;

namespace Shared
{
    public interface INetMessageHandler
    {
        bool ProcessTick( NetMessageTick msg );
        bool ProcessSignonState( NetMessageSignonState msg );
    }
}