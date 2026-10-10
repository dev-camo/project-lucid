using UnityEngine;

namespace HardlightProject
{
    public interface IResetCapableGameplayElement
    {
        void ResetGameplayElement();
        Vector3 GetPosition();
        bool IsDestroyed();
    }
}
