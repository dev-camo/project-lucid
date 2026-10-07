using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLocalPlayerMacOS : INativeGameCenterLocalPlayer
    {
        // Original imports resolve HLMacCore. They remain platform imports;
        // rebuilding this source does not establish that the library can load.
        [DllImport("HLMacCore")]
        private static extern bool Unity_Authenticated();
        [DllImport("HLMacCore")]
        private static extern string Unity_GamePlayerID();
        [DllImport("HLMacCore")]
        private static extern bool Unity_Underage();
        [DllImport("HLMacCore")]
        private static extern bool Unity_MultiplayerGamingRestricted();
        [DllImport("HLMacCore")]
        private static extern bool Unity_PersonalisedCommunicationRestricted();
        [DllImport("HLMacCore")]
        private static extern bool Unity_InitialiseGameCenterLocalPlayer(GameCenterLocalPlayerListenerMacOS.UnityCallback callback, string separator);
        [DllImport("HLMacCore")]
        private static extern bool Unity_DeinitialiseGameCenterLocalPlayer();
        [DllImport("HLMacCore")]
        private static extern void Unity_RegisterAuthenticateHandler();
        [DllImport("HLMacCore")]
        private static extern void Unity_UnregisterAuthenticateHandler();
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadPhotoForSize(int hashCode, long photoSize, string identifier);
        [DllImport("HLMacCore")]
        private static extern int Unity_GetLastPhotoForSizeLoadedPixelHeight();
        [DllImport("HLMacCore")]
        private static extern int Unity_GetLastPhotoForSizeLoadedPixelWidth();
        [DllImport("HLMacCore")]
        private static extern string Unity_GetPlayerPropertiesJoin(string identifier);
        [DllImport("HLMacCore")]
        private static extern string Unity_GetLocalPlayerPropertiesJoin();
        [DllImport("HLMacCore")]
        private static extern void Unity_IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback);

        // Original 0x06000771..0x06000775.
        public bool Authenticated => Unity_Authenticated();
        public string GamePlayerID => Unity_GamePlayerID();
        public bool Underage => Unity_Underage();
        public bool MultiplayerGamingRestricted => Unity_MultiplayerGamingRestricted();
        public bool PersonalisedCommunicationRestricted => Unity_PersonalisedCommunicationRestricted();

        // Original 0x06000776; gameObjectName is unused on this platform,
        // and a fresh callback delegate is passed each time. Native bool ignored.
        public void Initialise(string gameObjectName, string separator) =>
            Unity_InitialiseGameCenterLocalPlayer(new GameCenterLocalPlayerListenerMacOS.UnityCallback(GameCenterLocalPlayerListenerMacOS.Callback), separator);

        // Original 0x06000777..0x0600077f.
        public void Deinitialise() => Unity_DeinitialiseGameCenterLocalPlayer();
        public void RegisterAuthenticateHandler() => Unity_RegisterAuthenticateHandler();
        public void UnregisterAuthenticateHandler() => Unity_UnregisterAuthenticateHandler();
        public string GetPlayerPropertiesJoin(string identifier) => Unity_GetPlayerPropertiesJoin(identifier);
        public string GetLocalPlayerPropertiesJoin() => Unity_GetLocalPlayerPropertiesJoin();
        public void LoadPhotoForSize(int hashCode, PhotoSize photoSize, string identifier) => Unity_LoadPhotoForSize(hashCode, (long)photoSize, identifier);
        public int GetLastPhotoForSizeLoadedPixelHeight() => Unity_GetLastPhotoForSizeLoadedPixelHeight();
        public int GetLastPhotoForSizeLoadedPixelWidth() => Unity_GetLastPhotoForSizeLoadedPixelWidth();
        public void IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback) =>
            Unity_IssueAchievementChallenge(achievementId, message, callback);

        // Original 0x06000780; Object base only.
        public GameCenterLocalPlayerMacOS() { }
    }
}
