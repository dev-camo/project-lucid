using System;
using System.Collections;
using UnityEngine;

namespace Hardlight
{
    public readonly struct Player : IEquatable<Player>
    {
        public readonly string GamePlayerID;
        public readonly string DisplayName;
        private const int PlayerToParsePropertiesCount = 2;
        private readonly IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private readonly string m_identifier;

        // Original 0x060007a4; identifier is stored before the holder.
        public Player(string gamePlayerID, string displayName, string identifier, IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder)
        {
            GamePlayerID = gamePlayerID;
            DisplayName = displayName;
            m_identifier = identifier;
            m_gameCenterLocalPlayerReferencesHolder = gameCenterLocalPlayerReferencesHolder;
        }

        // Original 0x060007a5; out is reset before validation. Split retains
        // empty entries, and only the exact two-entry length is accepted.
        public static bool TryParseProperties(string propertiesJoinToIdentifyPlayer, string identifier,
            IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder, out Player player)
        {
            player = default;
            if (string.IsNullOrEmpty(propertiesJoinToIdentifyPlayer) || string.IsNullOrEmpty(identifier) || gameCenterLocalPlayerReferencesHolder == null)
                return false;
            string[] properties = propertiesJoinToIdentifyPlayer.Split(GameCenterLocalPlayer.SeparatorArray, StringSplitOptions.None);
            if (properties.Length != PlayerToParsePropertiesCount) return false;
            player = new Player(properties[0], properties[1], identifier, gameCenterLocalPlayerReferencesHolder);
            return true;
        }

        // Original 0x060007a6 and iterator 0x060007b3. Subscription cleanup is
        // on normal completion only: the original Dispose is empty, and faults
        // or abandoned enumeration leave the subscriptions intact.
        public IEnumerator LoadPhotoForSize(PhotoSize photoSize, Action<bool, Texture2D> onLoadPhotoForSizeCompleted)
        {
            if (!m_gameCenterLocalPlayerReferencesHolder.AreGameCenterLocalPlayerReferencesValid)
            {
                onLoadPhotoForSizeCompleted?.Invoke(false, null);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            Texture2D photo = Texture2D.whiteTexture;
            IGameCenterLocalPlayerListener gameCenterLocalPlayerListener = m_gameCenterLocalPlayerReferencesHolder.GameCenterLocalPlayerListener;
            gameCenterLocalPlayerListener.OnLoadPhotoForSize += LoadPhotoForSizeCallback;
            bool loadSuccess = false;
            bool loadPhotoForSizeCompleted = false;
            gameCenterLocalPlayerListener.OnLoadPhotoForSizeCompleted += LoadPhotoForSizeCompletedEvent;
            m_gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer.LoadPhotoForSize(hashCodeToCheck, photoSize, m_identifier);
            while (!loadPhotoForSizeCompleted) yield return null;
            gameCenterLocalPlayerListener.OnLoadPhotoForSize -= LoadPhotoForSizeCallback;
            gameCenterLocalPlayerListener.OnLoadPhotoForSizeCompleted -= LoadPhotoForSizeCompletedEvent;
            onLoadPhotoForSizeCompleted?.Invoke(loadSuccess, photo);

            // Original local functions 0x060007af/0x060007b0; unrelated hashes
            // leave the stored photo and both completion flags unchanged.
            void LoadPhotoForSizeCallback(int hashCode, Texture2D texture2D)
            {
                if (hashCodeToCheck == hashCode) photo = texture2D;
            }
            void LoadPhotoForSizeCompletedEvent(int hashCode, bool success)
            {
                if (hashCodeToCheck == hashCode)
                {
                    loadSuccess = success;
                    loadPhotoForSizeCompleted = true;
                }
            }
        }

        // Original 0x060007a7; instance string.Equals retains a null fault.
        private bool MatchIdentifier(string identifier) => m_identifier.Equals(identifier);
        // Original 0x060007a8-0x060007ac; equality compares identifier hash
        // codes, including collisions. Default values retain null faults.
        public static bool operator ==(Player lhs, Player rhs) => lhs.GetHashCode() == rhs.GetHashCode();
        public static bool operator !=(Player lhs, Player rhs) => lhs.GetHashCode() != rhs.GetHashCode();
        public bool Equals(Player other) => GetHashCode() == other.GetHashCode();
        public override bool Equals(object obj) => obj is Player other && this == other;
        public override int GetHashCode() => m_identifier.GetHashCode();
        // Original 0x060007ad; four-string Concat with the original literals.
        public override string ToString() => "GamePlayerID:" + GamePlayerID + " DisplayName:" + DisplayName;
    }
}
