using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200073a. A direction influencer whose speed also falls off with the
    // character's distance from this transform.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDirectionInfluencerFalloff : CharacterDirectionInfluencer
    {
        [Tooltip("X Axis = distance of character position to this position\nY axis = Value multiplied by speed.")]
        [SerializeField]
        private AnimationCurve m_normalisedSpeedOverDistance = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Character position will be considered snapped to the axis of this transform for the distance check.")]
        [SerializeField]
        private SnapToAxis m_distanceCheckSnap;

        // 060028bb; native code inlines the base speed.
        protected override float GetSpeed(Character character, DirectionInfluenceData influenceData)
        {
            Vector3 snapped = m_distanceCheckSnap.Snap(character.WorldPosition, transform.position);
            float distance = Vector3.Distance(snapped, transform.position);
            return base.GetSpeed(character, influenceData) * m_normalisedSpeedOverDistance.Evaluate(distance);
        }

        // 060028bc
        public CharacterDirectionInfluencerFalloff() { }
    }
}
