using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameUnlockRewardTracksFeature : MetaGameUnlockBase
    {
        private const string ID = "MetaGameUnlockRewardTracksFeature";
        private readonly UIWidgetProgression m_uiWidget;

        // Original06002ad4.
        public MetaGameUnlockRewardTracksFeature(UIWidgetProgression uiWidget)
        {
            m_uiWidget = uiWidget;
        }

        // Original06002ad5/ad6/ad7: literal ID, original key0x844fe221, and direct widget.
        public override string GetId() => ID;
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.RewardTracksFeature;
        public override UIWidgetProgression GetWidget() => m_uiWidget;

        // Original06002ad8: saveManager is unused; no save write is inserted.
        public override void Save(SaveManager saveManager)
        {
            ProcessManager.GetSystem<ChallengeManager>(null, true).SetRewardTracksFeatureUnlockedInterstitialSeen();
        }

        // Original06002ad9/ada: both original values are true.
        public override bool CanExitToMenu() => true;
        public override bool UsesAlternativeExitToMenuEvent() => true;
    }
}
