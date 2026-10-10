using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionScorerStreakObjective : MissionScorerObjective
    {
        [SerializeField, Tooltip("Horizontal and vertical distance to include other entities in a streak.")]
        private Vector2 m_range = Vector2.one;
        public MissionScorerStreakObjective() { }
    }
}
