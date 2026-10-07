using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLocalPlayerListenerStub : IGameCenterLocalPlayerListener, IGameCenterLocalPlayerListenerCallbackHandler
    {
        // Original 0x06000745.
        public string Name => null;

        public event Action<int, Texture2D> OnLoadPhotoForSize;
        public event Action<int, bool> OnLoadPhotoForSizeCompleted;

        // Original 0x0600074a/0x0600074b; these accessors use ordinary delegate
        // assignment. Adding a subscriber invokes the entire resulting delegate,
        // including previously registered subscribers, with true.
        public event Action<bool> OnAuthenticateHandler
        {
            add
            {
                m_onAuthenticateHandler += value;
                m_onAuthenticateHandler?.Invoke(true);
            }
            remove => m_onAuthenticateHandler -= value;
        }

        public event Action<string> OnPlayerDidChange;
        private Action<bool> m_onAuthenticateHandler;

        // Original 0x0600074e; registered delegates remain intact.
        public void Clear() { }
        // Original 0x0600074f-0x06000752.
        public void TriggerLoadPhotoForSizeEvent(int hashCode, Texture2D photo) => OnLoadPhotoForSize?.Invoke(hashCode, photo);
        public void TriggerLoadPhotoForSizeCompletedEvent(int hashCode, bool success) => OnLoadPhotoForSizeCompleted?.Invoke(hashCode, success);
        public void TriggerAuthenticateHandlerEvent(bool success) => m_onAuthenticateHandler?.Invoke(success);
        public void TriggerPlayerDidChange(string identifier) => OnPlayerDidChange?.Invoke(identifier);
        // Original 0x06000753.
        public GameCenterLocalPlayerListenerStub() { }
    }
}
