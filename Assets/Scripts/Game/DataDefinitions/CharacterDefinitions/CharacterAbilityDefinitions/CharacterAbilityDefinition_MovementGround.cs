using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004ea; complete 6 own fields/2 own methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbilityDefinition_MovementGround : CharacterAbilityDefinition_MovementFree
    {
        // 0x0400118e; original instance offset 0xfc.
        [Header("Ground settings")]
        [Tooltip("Minimum time to stay in the state.")]
        public float StateTimeMin;
        // 0x0400118f; original instance offset 0x100.
        [Tooltip("Time to stay in landing logic.")]
        public float LandingTime = 0.2f;
        // 0x04001190; original instance offset 0x108.
        [Tooltip("Braking parameters when approaching a ledge.")]
        public LedgeBrakeParameters LedgeBrakeParameters;
        // 0x04001191; original instance offset 0x110.
        [Tooltip("If true, override the character constants MovementStationarySlopeAngleMax and MovementOrientateToSlopeAngleMax while ability is active.")]
        public bool OverrideSlopeAngleMaxConstants;
        // 0x04001192; original instance offset 0x114.
        [ShowIf("OverrideSlopeAngleMaxConstants", (string)null)]
        [Tooltip("Value to use to override the character constant MovementStationarySlopeAngleMax.")]
        public float MovementStationarySlopeAngleMaxOverride;
        // 0x04001193; original instance offset 0x118.
        [Tooltip("Value to use to override the character constant MovementOrientateToSlopeAngleMax.")]
        [ShowIf("OverrideSlopeAngleMaxConstants", (string)null)]
        public float MovementOrientateToSlopeAngleMaxOverride;

        // 0x06001b48: base cache runs first; both architectures inline the exact
        // original LedgeBrakeParameters.CalculateCachedValues body afterward.
        // The original null fault occurs after base caching has already run.
        public override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            LedgeBrakeParameters.CalculateCachedValues();
        }

        // 0x06001b49: LandingTime is 0.2 before the real base constructor chain.
        // No parameter object is allocated; StateTimeMin and slope overrides stay zero.
        protected CharacterAbilityDefinition_MovementGround() { }
    }
}
