namespace Shared.Enums
{
    public enum ENetCommand
    {
        NetNop = 0,
        NetDisconnect = 1,
        NetFile = 2,
        NetTick = 3,
        NetStringCmd = 4,
        NetSetConVar = 5,
        NEtSignonState = 6,

        SvcPrint = 7,
        SvcServerInfo = 8,
        SvcSendTable = 9,
        SvcClassInfo = 10,
        SvcSetPause = 11,
        SvcCreateStringTable = 12,
        SvcUpdateStringTable = 13,

        SvcVoiceInit = 14,
        SvcVoiceData = 15,

        SvcSounds = 17,
        SvcSetView = 18,
        SvcFixAngle = 19,
        SvcCrosshairAngle = 20,

        SvcUserMessage = 21,
        SvcEntityMessage = 22,
        SvcGameEvent = 23,
        SvcPacketEntities = 24,
        SvcTempEntities = 25,

        ClcClientIfo = 8,
        ClcMove = 9,
        ClcVoiceData = 10,
        ClcBaselineAck = 11,
        ClcListenEvents = 12,
        ClcRespondCvarValue = 13,
        ClcFileCrcCheck = 14,
    }
}