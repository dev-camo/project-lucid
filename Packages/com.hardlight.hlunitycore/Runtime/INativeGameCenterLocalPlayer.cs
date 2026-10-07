namespace Hardlight
{
    public interface INativeGameCenterLocalPlayer
    {
        void Initialise(string gameObjectName, string separator);
        void Deinitialise();
        bool Authenticated { get; }
        string GamePlayerID { get; }
        bool Underage { get; }
        bool MultiplayerGamingRestricted { get; }
        bool PersonalisedCommunicationRestricted { get; }
        void RegisterAuthenticateHandler();
        void UnregisterAuthenticateHandler();
        string GetPlayerPropertiesJoin(string identifier);
        string GetLocalPlayerPropertiesJoin();
        void LoadPhotoForSize(int hashCode, PhotoSize photoSize, string identifier);
        int GetLastPhotoForSizeLoadedPixelHeight();
        int GetLastPhotoForSizeLoadedPixelWidth();
        void IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback);
    }
}
