using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbilityDefinition_BurstAttackChaos : CharacterAbilityDefinition_BurstAttack
    {
        [Tooltip("The time categories to animate over time.")]
        public List<TimeCategory> TimeCategories = new List<TimeCategory>();
        [Tooltip("Defines the time dilation animation and event triggers.")]
        public List<CharacterAbilityDefinition_BurstAttackChaos.ChaosTimeDilation> TimeDilations = new List<ChaosTimeDilation>();

        protected CharacterAbilityDefinition_BurstAttackChaos() { }

        [Serializable]
        public class ChaosTimeDilation
        {
            public AnimationCurve TimeScaleAnimation = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            public string OnTriggerFSMName;
            public int RechargeIndex;
            public ActorAnimationDefinition AnimationDefinition;
            public ChaosTimeDilation() { }
        }
    }
}
