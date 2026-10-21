using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b4.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementChallengesOpen", menuName = "HardlightProject/Requirements/RequirementChallengesOpen")]
    public class RequirementChallengesOpen : RequirementGameplayLevelBase
    {
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);

        // 0x06001f13 (x86-64 0x557b70): the stored flag is read from the App
        // storage; an absent key reads as false. A missing App faults.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            return m_appRef.Get().Storage.TryGetValue(AppFSMKeys.ChallengesActive, out bool challengesActive) &&
                challengesActive;
        }

        // 0x06001f14.
        public RequirementChallengesOpen() { }
    }
}
