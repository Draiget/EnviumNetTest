namespace Shared.Filter
{
    public interface IRecipientFilter
    {
        bool IsReliable();
        bool IsInitMessage();
    }
}