using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    public interface IHomingAbility // 02000310, all eight abstract accessor/method rows.
    {
        void Reset();
        bool CanActivate { get; }
        bool IsTriggered { get; set; }
        bool OverridesTargeting { get; }
        bool ShouldProjectFromCamera();
        float GetAdditionalSpeed(float time);
        CharacterAbilityDefinition_Targeting TargetingDefinition { get; }
    }
}
