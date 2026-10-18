using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020004ba; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityDefinition_AirStompAttack : CharacterAbilityDefinition_Air
    {
        [Header("Air stomp turn and boost")]
        [SerializeField]
        private List<AirStompSlopeSetting> m_airStompSlopeSettings;

        public override Type ScriptType => typeof(CharacterAbility_AirStompAttack);

        // 06001add (1.10.1 RVA 0x5422F0). The first setting whose [min, max) range
        // contains the angle wins; the comparisons are written the way the native
        // code orders them so that a NaN angle matches the first entry.
        public void GetSettingsForSlope(float slopeAngle, out bool orientDownhill, out float boostSpeed)
        {
            orientDownhill = false;
            boostSpeed = 0f;
            foreach (AirStompSlopeSetting setting in m_airStompSlopeSettings)
            {
                if (setting.MinAngleInclusive > slopeAngle)
                    continue;
                if (slopeAngle >= setting.MaxAngleExclusive)
                    continue;
                orientDownhill = setting.OrientDownhill;
                boostSpeed = setting.BoostIfDownhill ? setting.BoostSpeed : 0f;
                break;
            }
        }

        public CharacterAbilityDefinition_AirStompAttack() { }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class AirStompSlopeSetting
        {
            [Tooltip("When landing, minimum angle of slope for these settings to apply (inclusive).")]
            [Range(0f, 90f)]
            public float MinAngleInclusive;
            [Tooltip("When landing, maximum angle of slope for these settings to apply (exclusive).")]
            [Range(0f, 90f)]
            public float MaxAngleExclusive;
            [Tooltip("If true, the character will be oriented to face downhill on landing.")]
            public bool OrientDownhill;
            [Tooltip("If the character is facing downhill, set their forward velocity to BoostSpeed.")]
            public bool BoostIfDownhill;
            [ShowIf("BoostIfDownhill", (string)null)]
            [Range(0f, 100f)]
            [Tooltip("Forward velocity to set on landing if BoostIfDownhill is active.")]
            public float BoostSpeed;

            public AirStompSlopeSetting() { }
        }
    }
}
