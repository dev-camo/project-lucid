using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000786: a spawned scorer prefab that scores through its objective and then completes it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MissionScorerInstance : MonoBehaviour
    {
        [SerializeField] private MissionScorerEvaluator m_evaluator;

        public MissionScorerEvaluator Evaluator => m_evaluator;
        public MissionScorerObjective Objective { get; set; }

        // 06002a36 (0x5D6A90): evaluate with the other transform, then complete the objective.
        public void Action_OnProximityTargetEnter(Transform other)
        {
            Objective.Evaluate(other);
            Objective.Action_ObjectiveComplete();
        }

        // 06002a37 (0x5D6AC0): the authored score replaces evaluation, then the objective completes.
        public void Action_OnSpeedCheckEnter(float scoreToAdd)
        {
            Objective.SetScoreToAdd(scoreToAdd);
            Objective.Action_ObjectiveComplete();
        }

        public MissionScorerInstance() { }
    }
}
