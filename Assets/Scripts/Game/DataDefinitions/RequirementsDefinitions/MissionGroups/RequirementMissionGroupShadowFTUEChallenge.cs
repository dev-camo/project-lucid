using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "RequirementMissionGroupShadowFTUEChallenge", menuName = "HardlightProject/DefinitionData/Definitions/RequirementMissionGroupShadowFTUEChallenge")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementMissionGroupShadowFTUEChallenge : RequirementMissionGroupBase
    {
        // Original 0x06001f29 obtains the normal auto-registering App reference
        // with null context. The field remains mutable, as shipped.
        private static SystemRef<App> s_appRef = ProcessManager.GetSystemRef<App>();

        // Original 0x06001f27: neither authored argument is read. Missing App
        // returns false; a present App with null Storage retains its fault.
        // Explicit storeDefault=false avoids creating a state-machine entry
        // while asking whether the original Shadow tutorial is selected.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition levelDefinition)
        {
            return s_appRef.TryGet(out App app) &&
                app.Storage.GetValue<bool>(AppFSMKeys.SelectShadowFTUEChallenge, false, false);
        }

        // Original 0x06001f28 has only the inherited ScriptableObject path.
        public RequirementMissionGroupShadowFTUEChallenge() { }
    }
}
