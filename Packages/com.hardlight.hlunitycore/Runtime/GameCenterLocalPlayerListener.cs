using System;
using System.Text;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterLocalPlayerListener : MonoBehaviour, IGameCenterLocalPlayerListener
    {
        // Original 0x0600071c; Component.gameObject precedes Object.name.
        public string Name => gameObject.name;

        public event Action<int, Texture2D> OnLoadPhotoForSize;
        public event Action<int, bool> OnLoadPhotoForSizeCompleted;
        public event Action<bool> OnAuthenticateHandler;
        public event Action<string> OnPlayerDidChange;
        private IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private int m_hashCode;

        // Original 0x06000725; first nonnull reference wins until Clear.
        public void SetGameCenterLocalPlayerReferencesHolder(IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder)
        {
            if (gameCenterLocalPlayerReferencesHolder != null && m_gameCenterLocalPlayerReferencesHolder == null)
                m_gameCenterLocalPlayerReferencesHolder = gameCenterLocalPlayerReferencesHolder;
        }
        // Original 0x06000726; delegates and hash remain intact.
        public void Clear() => m_gameCenterLocalPlayerReferencesHolder = null;

        // Original 0x06000727; allocation precedes the string check. Both
        // shipping forms pass TextureFormat literal 53, UTF8 raw bytes, Apply().
        // Failed conversion does not destroy the allocated texture.
        private static bool TryGetTexture2D(string imageDataString, int height, int width, out Texture2D texture2D)
        {
            texture2D = new Texture2D(width, height, (TextureFormat)53, false);
            if (string.IsNullOrEmpty(imageDataString)) return false;
            byte[] imageData = Encoding.UTF8.GetBytes(imageDataString);
            if (imageData.Length == 0) return false;
            texture2D.LoadRawTextureData(imageData);
            texture2D.Apply();
            return true;
        }
        // Original 0x06000728; failed parsing writes zero to m_hashCode.
        public void SetHashCode(string hashCodeString) => int.TryParse(hashCodeString, out m_hashCode);

        // Original 0x06000729; capture one native reference, height before
        // width, texture conversion before reading the photo event.
        public void LoadPhotoForSize(string photoDataString)
        {
            INativeGameCenterLocalPlayer nativeGameCenterLocalPlayer = m_gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer;
            if (TryGetTexture2D(photoDataString, nativeGameCenterLocalPlayer.GetLastPhotoForSizeLoadedPixelHeight(),
                nativeGameCenterLocalPlayer.GetLastPhotoForSizeLoadedPixelWidth(), out Texture2D texture2D))
                OnLoadPhotoForSize?.Invoke(m_hashCode, texture2D);
        }
        // Original 0x0600072a/0x0600072b; invalid bool strings suppress events.
        public void LoadPhotoForSizeCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadPhotoForSizeCompleted?.Invoke(m_hashCode, success);
        }
        public void AuthenticateHandler(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnAuthenticateHandler?.Invoke(success);
        }
        // Original 0x0600072c.
        public void PlayerDidChange(string gamePlayerID) => OnPlayerDidChange?.Invoke(gamePlayerID);
        // Original 0x0600072d; MonoBehaviour constructor only.
        public GameCenterLocalPlayerListener() { }
    }
}
