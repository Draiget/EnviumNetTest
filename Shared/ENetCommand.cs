namespace Shared
{
    public enum ENetCommand
    {
        Nop = 0,
        Disconnect = 1,
        File = 2,
        Tick = 3,
        StringCmd = 5,
        SetConVar = 6,
        SignonState = 7,
    }
}