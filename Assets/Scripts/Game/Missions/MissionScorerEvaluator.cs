using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000711: one genuine abstract API and a
    // protected base-only constructor, with no substitute evaluator behavior.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MissionScorerEvaluator : MonoBehaviour
    {
        public abstract float Evaluate(MissionObjective missionObjective, Transform otherTransform,
            out ActorAnimationDefinition animationDefinition);

        protected MissionScorerEvaluator()
        {
        }
    }
}
