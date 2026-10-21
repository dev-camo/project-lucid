using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005bd.
    [CreateAssetMenu(fileName = "RequirementMissionGroupShadowFTUEAvailable", menuName = "HardlightProject/DefinitionData/Definitions/RequirementMissionGroupShadowFTUEAvailable")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementMissionGroupShadowFTUEAvailable : RequirementMissionGroupBase
    {
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef =
            ProcessManager.GetSystemRef<ChallengeManager>(null, true);

        // 0x06001f25 (x86-64 0x558e40): available once the challenges feature is
        // unlocked and the Shadow FTUE has already been seen or completed. The
        // FTUE query is evaluated before the feature state is read; neither
        // argument is read.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition levelDefinition)
        {
            if (!m_challengeManagerRef.TryGet(out ChallengeManager challengeManager)) return false;
            bool shouldShowShadowFtue = challengeManager.ShouldShowShadowFTUE();
            return !shouldShowShadowFtue &&
                challengeManager.FeatureState != ChallengeManager.ChallengeFeatureState.Locked;
        }

        // 0x06001f26.
        public RequirementMissionGroupShadowFTUEAvailable() { }
    }
}
