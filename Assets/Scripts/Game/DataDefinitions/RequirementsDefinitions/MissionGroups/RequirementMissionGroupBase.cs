using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005bb: genuine abstract authored requirement.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class RequirementMissionGroupBase : ScriptableObject
    {
        // 0x06001f21: an actual abstract method, with no original native body.
        public abstract bool RequirementsMet(MissionGroup missionGroup,
            GameplayLevelDefinition levelDefinition);

        // 0x06001f22: original ScriptableObject construction only.
        protected RequirementMissionGroupBase() { }
    }
}
