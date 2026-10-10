using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class DebugUnlockLevels
    {
        // Game.Runtime 0x06002001: original beforefieldinit cctor calls the real ProcessManager.
        private static readonly SystemRef<SaveManager> s_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);

        // Game.Runtime 0x06001ff9: genuine shipped player RET.
        public static void AddButton() { }
        // Game.Runtime 0x06001ffa: original player is mov w0,0;ret, regardless of saved debug setting.
        public static bool AreAllLevelsUnlocked() => false;

        // Game.Runtime 0x06001ffb/0x06002004: original cached non-capturing callback.
        public static void Activate() => s_saveManagerRef.InvokeOnValid(saveManager => SaveSettings(saveManager, true));
        // Game.Runtime 0x06001ffc/0x06002005: original separate cached non-capturing callback.
        public static void Deactivate() => s_saveManagerRef.InvokeOnValid(saveManager => SaveSettings(saveManager, false));
        // Game.Runtime 0x06001ffd: method-group callback, allocated without a lambda cache in the original.
        private static void Toggle() => s_saveManagerRef.InvokeOnValid(ToggleAndSaveSettings);

        // Game.Runtime 0x06001ffe: read the actual saved setting, then reload settings in SaveSettings.
        private static void ToggleAndSaveSettings(SaveManager saveManager)
        {
            bool unlock = !saveManager.GetSaveDataSettings().Debug.UnlockLevels;
            SaveSettings(saveManager, unlock);
        }

        // Game.Runtime 0x06001fff: setter executes before the real save request, with no readiness/null guard.
        private static void SaveSettings(SaveManager saveManager, bool unlock)
        {
            saveManager.GetSaveDataSettings().Debug.UnlockLevels = unlock;
            saveManager.RequestSaveSettings();
        }

        // Game.Runtime 0x06002000: native inlining of original always-false query concatenates OFF.
        private static string GetButtonName() => "Unlocked all levels: " + (AreAllLevelsUnlocked() ? "ON" : "OFF");
    }
}
