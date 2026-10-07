using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterLocalPlayerListenerMacOS : IGameCenterLocalPlayerListener
    {
        public delegate void UnityCallback(IntPtr methodNamePtr, IntPtr methodParamPtr);
        // Original 0x0600072e; this platform listener has no GameObject name.
        public string Name => null;

        public event Action<int, Texture2D> OnLoadPhotoForSize;
        public event Action<int, bool> OnLoadPhotoForSizeCompleted;
        public event Action<bool> OnAuthenticateHandler;
        public event Action<string> OnPlayerDidChange;
        private IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private int m_hashCode;
        private static IReadOnlyDictionary<string, Action<string>> s_callbackMap;

        // Original 0x06000737; publish the new static map only after all five
        // ordered entries are added. A later listener replaces the shared map.
        public GameCenterLocalPlayerListenerMacOS()
        {
            s_callbackMap = new Dictionary<string, Action<string>>
            {
                { "SetHashCode", SetHashCode },
                { "LoadPhotoForSize", LoadPhotoForSize },
                { "LoadPhotoForSizeCompleted", LoadPhotoForSizeCompleted },
                { "AuthenticateHandler", AuthenticateHandler },
                { "PlayerDidChange", PlayerDidChange }
            };
        }

        // Original 0x06000738; both pointers are converted before the map read.
        // Missing names log; a present null callback faults on invocation.
        [MonoPInvokeCallback(typeof(UnityCallback))]
        public static void Callback(IntPtr methodNamePtr, IntPtr methodParamPtr)
        {
            string methodName = Marshal.PtrToStringAuto(methodNamePtr);
            string methodParam = Marshal.PtrToStringAuto(methodParamPtr);
            if (s_callbackMap.TryGetValue(methodName, out Action<string> method))
                method(methodParam);
            else
                HLOutput.LogError("[GameCenterLocalPlayer] GameCenterLocalPlayerCallback - No method with name '" + methodName + "'.");
        }

        // Original 0x06000739; first nonnull reference wins until Clear.
        public void SetGameCenterLocalPlayerReferencesHolder(IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder)
        {
            if (gameCenterLocalPlayerReferencesHolder != null && m_gameCenterLocalPlayerReferencesHolder == null)
                m_gameCenterLocalPlayerReferencesHolder = gameCenterLocalPlayerReferencesHolder;
        }

        // Original 0x0600073a; event fields, hash and static map remain intact.
        public void Clear() => m_gameCenterLocalPlayerReferencesHolder = null;

        // Original 0x0600073b; allocation precedes the string check. Both native
        // forms pass TextureFormat literal 53, raw UTF8 bytes and Apply().
        // No Base64 conversion, disposal or cleanup is present on failure.
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

        // Original 0x0600073c; failed parsing resets the stored hash through out.
        private void SetHashCode(string hashCodeString) => int.TryParse(hashCodeString, out m_hashCode);

        // Original 0x0600073d; one native reference, height before width, then
        // texture construction. No holder validity check or event-first guard.
        private void LoadPhotoForSize(string photoDataString)
        {
            INativeGameCenterLocalPlayer nativeGameCenterLocalPlayer = m_gameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer;
            if (TryGetTexture2D(photoDataString, nativeGameCenterLocalPlayer.GetLastPhotoForSizeLoadedPixelHeight(),
                nativeGameCenterLocalPlayer.GetLastPhotoForSizeLoadedPixelWidth(), out Texture2D texture2D))
                OnLoadPhotoForSize?.Invoke(m_hashCode, texture2D);
        }

        // Original 0x0600073e/0x0600073f; invalid bool strings suppress events.
        private void LoadPhotoForSizeCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadPhotoForSizeCompleted?.Invoke(m_hashCode, success);
        }
        private void AuthenticateHandler(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnAuthenticateHandler?.Invoke(success);
        }

        // Original 0x06000740.
        private void PlayerDidChange(string gamePlayerID) => OnPlayerDidChange?.Invoke(gamePlayerID);
    }
}
