using System.Globalization;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameUnlockChallengesFeature : MetaGameUnlockBase
    {
        private readonly ChallengeCycleDefinition m_challengeCycleDefinition;

        // Original06002a6e: base construction precedes the readonly assignment.
        public MetaGameUnlockChallengesFeature(ChallengeCycleDefinition challengeCycleDefinition)
        {
            m_challengeCycleDefinition = challengeCycleDefinition;
        }

        // Original06002a6f: the date value is read before resolving invariant culture.
        public override string GetId() => m_challengeCycleDefinition.CycleBeginsFrom.ToString(CultureInfo.InvariantCulture);

        // Original06002a70/71: original key0x3a6919b4 and direct widget field.
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.ChallengesFeatureUnlocked;
        public override UIWidgetProgression GetWidget() => m_challengeCycleDefinition.ProgressionWidget;

        // Original06002a72: saveManager is unused; true manager lookup uses null and true.
        public override void Save(SaveManager saveManager)
        {
            ProcessManager.GetSystem<ChallengeManager>(null, true).SetFeatureUnlockedInterstitialSeen();
        }

        // Original06002a73.
        public override bool CanExitToMenu() => true;
    }
}
