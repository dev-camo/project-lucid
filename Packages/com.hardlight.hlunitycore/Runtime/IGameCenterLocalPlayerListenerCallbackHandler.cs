using UnityEngine;

namespace Hardlight
{
    public interface IGameCenterLocalPlayerListenerCallbackHandler
    {
        void TriggerLoadPhotoForSizeEvent(int hashCode, Texture2D photo);
        void TriggerLoadPhotoForSizeCompletedEvent(int hashCode, bool success);
        void TriggerAuthenticateHandlerEvent(bool success);
        void TriggerPlayerDidChange(string identifier);
    }
}
