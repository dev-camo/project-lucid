using System;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Offline
{
    internal sealed class LocalAccountListener : IGameCenterLocalPlayerListener, IGameCenterLocalPlayerListenerCallbackHandler
    {
        public string Name => null;
        public event Action<int, Texture2D> OnLoadPhotoForSize;
        public event Action<int, bool> OnLoadPhotoForSizeCompleted;
        public event Action<bool> OnAuthenticateHandler;
        public event Action<string> OnPlayerDidChange;
        public void Clear()
        {
            OnLoadPhotoForSize = null;
            OnLoadPhotoForSizeCompleted = null;
            OnAuthenticateHandler = null;
            OnPlayerDidChange = null;
        }
        public void TriggerLoadPhotoForSizeEvent(int hashCode, Texture2D photo) => OnLoadPhotoForSize?.Invoke(hashCode, photo);
        public void TriggerLoadPhotoForSizeCompletedEvent(int hashCode, bool success) => OnLoadPhotoForSizeCompleted?.Invoke(hashCode, success);
        public void TriggerAuthenticateHandlerEvent(bool success) => OnAuthenticateHandler?.Invoke(success);
        public void TriggerPlayerDidChange(string identifier) => OnPlayerDidChange?.Invoke(identifier);
    }
}
