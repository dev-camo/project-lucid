namespace HardlightProject
{
    // Original Game.Runtime0x020007ac; two abstract contracts, zero native bodies.
    public interface ISaveGameListener
    {
        void OnSaveGameOpen(SaveDataGame saveDataGame);
        void OnSaveGameClose(SaveDataGame saveDataGame);
    }
}
