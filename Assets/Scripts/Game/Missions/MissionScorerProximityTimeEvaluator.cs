using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000715: the proximity score multiplied by a curve of the whole seconds the objective's
    // timer has left (rounded up).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionScorerProximityTimeEvaluator : MissionScorerProximityEvaluator
    {
        [Tooltip("Multiplies the evaluated score by time remaining lookup.")]
        [SerializeField]
        private AnimationCurve m_timeRemainingMultiplierCurve;

        // 0600279e (0x5C0D60)
        public override float Evaluate(MissionObjective missionObjective, Transform otherTransform,
            out ActorAnimationDefinition animationDefinition)
        {
            float score = base.Evaluate(missionObjective, otherTransform, out animationDefinition);
            return m_timeRemainingMultiplierCurve.Evaluate(Mathf.Ceil(missionObjective.GetTimeRemainingSeconds())) * score;
        }

        public MissionScorerProximityTimeEvaluator() { }
    }
}
