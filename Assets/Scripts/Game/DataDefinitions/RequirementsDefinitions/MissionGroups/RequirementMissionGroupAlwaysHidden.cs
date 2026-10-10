using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ba. The false result is present in both
    // shipping native backends; it is the authored rule, not a recovery stub.
    [CreateAssetMenu(fileName = "RequirementMissionGroupAlwaysHidden",
        menuName = "HardlightProject/DefinitionData/Definitions/RequirementMissionGroupAlwaysHidden")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementMissionGroupAlwaysHidden : RequirementMissionGroupBase
    {
        // 0x06001f1f: ARM64 0x53003c..0x530044; x86_64 0x558d30..0x558d40.
        public override bool RequirementsMet(MissionGroup missionGroup,
            GameplayLevelDefinition levelDefinition)
        {
            return false;
        }

        // 0x06001f20: original inherited ScriptableObject construction only.
        public RequirementMissionGroupAlwaysHidden() { }
    }
}
