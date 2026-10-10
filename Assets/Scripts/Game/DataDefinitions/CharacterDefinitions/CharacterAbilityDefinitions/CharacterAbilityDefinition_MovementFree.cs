using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004e9; complete 4 own fields/1 own methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbilityDefinition_MovementFree : CharacterAbilityDefinition_Movement
    {
        // 0x0400118a; original instance offset 0xe0.
        [Tooltip("When in movement, the turning trait properties.")]
        public CharacterTraits.TurnTraits Turn;
        // 0x0400118b; original instance offset 0xe8.
        [Header("Tracker movement")]
        [Tooltip("Directional influence from the currently tracked spline, depending on the character's current angle to it. Horizontal axis is the angle to the tracker. Vertical axis is the angle rate of change to return back to the tracked spline.")]
        public AnimationCurve SplineAngleInfluence;
        // 0x0400118c; original instance offset 0xf0.
        [Tooltip("The maximum turn angle away from the currently tracked spline as a function of velocity.")]
        public AnimationCurve SplineTurnAngleMax;
        // 0x0400118d; original instance offset 0xf8.
        [Tooltip("The maximum turn angle can be overriden by spline metadata.")]
        public bool SplineTurnAngleCanBeOverriden = true;

        // 0x06001b47: original true flag initialization precedes the real base chain.
        // Turn and both curves remain null. The inherited GravityMultiplier stays 1.
        protected CharacterAbilityDefinition_MovementFree() { }
    }
}
