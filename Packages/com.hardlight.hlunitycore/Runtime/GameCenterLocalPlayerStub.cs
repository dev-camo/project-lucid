using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterLocalPlayerStub : INativeGameCenterLocalPlayer
    {
        private readonly IGameCenterLocalPlayerListenerCallbackHandler m_gameCenterLocalPlayerListenerCallbackHandler;
        private readonly string m_gamePlayerID = Guid.NewGuid().ToString();
        private readonly string m_localPlayerPropertiesJoin;

        // Original 0x06000781-0x06000785; these are the shipping implementation.
        public bool Authenticated => true;
        public string GamePlayerID => m_gamePlayerID;
        public bool Underage => false;
        public bool MultiplayerGamingRestricted => false;
        public bool PersonalisedCommunicationRestricted => false;

        // Original 0x06000786; the GUID field initializer precedes Object's
        // constructor. The callback is stored before the local join is built.
        public GameCenterLocalPlayerStub(IGameCenterLocalPlayerListenerCallbackHandler gameCenterLocalPlayerListenerCallbackHandler)
        {
            m_gameCenterLocalPlayerListenerCallbackHandler = gameCenterLocalPlayerListenerCallbackHandler;
            m_localPlayerPropertiesJoin = string.Join(GameCenterLocalPlayer.Separator, new[] { m_gamePlayerID, "Stub Local Player" });
        }

        // Original 0x06000787/0x06000788.
        public void Initialise(string gameObjectName, string separator) { }
        public void Deinitialise() { }

        // Original 0x06000789; the callback receiver is not null-checked.
        public void RegisterAuthenticateHandler() => m_gameCenterLocalPlayerListenerCallbackHandler.TriggerAuthenticateHandlerEvent(true);
        // Original 0x0600078a.
        public void UnregisterAuthenticateHandler() { }

        // Original 0x0600078b; an unknown identifier receives a newly generated
        // identifier on each call. The array is allocated before NewGuid.
        public string GetPlayerPropertiesJoin(string identifier)
        {
            if (identifier == m_gamePlayerID) return m_localPlayerPropertiesJoin;
            return string.Join(GameCenterLocalPlayer.Separator, new[] { Guid.NewGuid().ToString(), "Stub Player" });
        }
        // Original 0x0600078c.
        public string GetLocalPlayerPropertiesJoin() => m_localPlayerPropertiesJoin;

        // Original 0x0600078d; the engine texture getter precedes the callback
        // receiver read. The receiver is read again for the completion event.
        public void LoadPhotoForSize(int hashCode, PhotoSize photoSize, string identifier)
        {
            Texture2D photo = Texture2D.whiteTexture;
            m_gameCenterLocalPlayerListenerCallbackHandler.TriggerLoadPhotoForSizeEvent(hashCode, photo);
            m_gameCenterLocalPlayerListenerCallbackHandler.TriggerLoadPhotoForSizeCompletedEvent(hashCode, true);
        }
        // Original 0x0600078e/0x0600078f.
        public int GetLastPhotoForSizeLoadedPixelHeight() => 0;
        public int GetLastPhotoForSizeLoadedPixelWidth() => 0;

        // Original 0x06000790.
        public void IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback) => callback?.Invoke(true, null);
    }
}
