using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b3.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementChallengesFeatureSeen", menuName = "HardlightProject/Requirements/RequirementChallengesFeatureSeen")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementChallengesFeatureSeen : RequirementGameplayLevelBase
    {
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef =
            ProcessManager.GetSystemRef<ChallengeManager>(null, true);

        // 0x06001f11 (x86-64 0x557a80): only the final feature state counts.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            return m_challengeManagerRef.TryGet(out ChallengeManager challengeManager) &&
                challengeManager.FeatureState == ChallengeManager.ChallengeFeatureState.UnlockedSeenFeature;
        }

        // 0x06001f12.
        public RequirementChallengesFeatureSeen() { }
    }
}
