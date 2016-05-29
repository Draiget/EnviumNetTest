namespace Server.Frames
{
    public interface IClientFrameManager
    {
        ClientFrame GetClientFrame(int tick, bool exact);
        int AddClientFrame(ClientFrame frame);
        int CountClientFrames();
        void RemoveOldestFrame();
        void DeleteClientFrames(int tick);
    }
}
