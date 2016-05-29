using Shared.Channel;

namespace Server.Clients
{
    public interface IClient
    {
        void Connect(string name, NetChannel channel);
        void Disconnect(string format, params object[] args);

        void Clear();
    }
}
