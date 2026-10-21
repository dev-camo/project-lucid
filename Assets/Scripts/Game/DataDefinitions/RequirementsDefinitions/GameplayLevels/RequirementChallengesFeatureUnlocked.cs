using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b2.
    [CreateAssetMenu(fileName = "RequirementChallengesFeatureUnlocked", menuName = "HardlightProject/Requirements/RequirementChallengesFeatureUnlocked")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RequirementChallengesFeatureUnlocked : RequirementGameplayLevelBase
    {
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef =
            ProcessManager.GetSystemRef<ChallengeManager>(null, true);

        // The original array initializer data is the int32 triple 1, 2, 3.
        private readonly ChallengeManager.ChallengeFeatureState[] m_unlockedStates =
        {
            ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial,
            ChallengeManager.ChallengeFeatureState.UnlockedSeenInterstitial,
            ChallengeManager.ChallengeFeatureState.UnlockedSeenFeature
        };

        // 0x06001f0f (x86-64 0x557910): every state past Locked counts as unlocked.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            return m_challengeManagerRef.TryGet(out ChallengeManager challengeManager) &&
                m_unlockedStates.Contains(challengeManager.FeatureState);
        }

        // 0x06001f10: both field initializers run before the ScriptableObject base.
        public RequirementChallengesFeatureUnlocked() { }
    }
}
