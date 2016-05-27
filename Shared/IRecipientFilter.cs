namespace Shared
{
    public interface IRecipientFilter
    {
        bool IsReliable();
        bool IsInitMessage();
    }
}