using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200096b; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OnScreenBurstAttackButton : MonoBehaviour
    {
        public readonly Bindable<bool> CanTrigger = new Bindable<bool>();
        public readonly Bindable<bool> IsChaosControl = new Bindable<bool>();
        public readonly Bindable<bool> IsActive = new Bindable<bool>();

        // 06003636 (1.10.1 RVA 0x68B9D0)
        public void SetTargetingActive(BurstAttackUpdateMessage burstAttackUpdateData)
        {
            CanTrigger.Value = burstAttackUpdateData.CanTrigger;
            IsChaosControl.Value = burstAttackUpdateData.IsChaosControl;
            IsActive.Value = burstAttackUpdateData.IsActive;
        }

        // 06003637 (RVA 0x68BA40)
        public OnScreenBurstAttackButton()
        {
        }
    }
}
