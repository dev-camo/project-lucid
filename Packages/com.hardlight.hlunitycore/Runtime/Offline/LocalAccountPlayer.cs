using System;
using Hardlight;

namespace ProjectLucid.Offline
{
    // Local identity is deliberately separate from a platform GamePlayerID.
    // Single default profile policy; multiple-profile migration is not proposed.
    public static class LocalProfileIdentity
    {
        public const string Identifier = "project-lucid:local:default";
        public const string DisplayName = "Local Player";
    }

    internal sealed class LocalAccountPlayer : INativeGameCenterLocalPlayer
    {
        private readonly IGameCenterLocalPlayerListenerCallbackHandler callbacks;
        internal LocalAccountPlayer(IGameCenterLocalPlayerListenerCallbackHandler callbacks)
        {
            this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        }
        public bool Authenticated => false;
        public string GamePlayerID => null;
        public bool Underage => false;
        public bool MultiplayerGamingRestricted => true;
        public bool PersonalisedCommunicationRestricted => true;
        public void Initialise(string gameObjectName, string separator) { }
        public void Deinitialise() { }
        public void RegisterAuthenticateHandler() => callbacks.TriggerAuthenticateHandlerEvent(false);
        public void UnregisterAuthenticateHandler() { }
        public string GetPlayerPropertiesJoin(string identifier) => null;
        public string GetLocalPlayerPropertiesJoin() => null;
        public void LoadPhotoForSize(int hashCode, PhotoSize photoSize, string identifier)
        {
            callbacks.TriggerLoadPhotoForSizeEvent(hashCode, null);
            callbacks.TriggerLoadPhotoForSizeCompletedEvent(hashCode, false);
        }
        public int GetLastPhotoForSizeLoadedPixelHeight() => 0;
        public int GetLastPhotoForSizeLoadedPixelWidth() => 0;
        public void IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback)
            => callback?.Invoke(false, "Platform achievement challenges are unavailable offline.");
    }
}
