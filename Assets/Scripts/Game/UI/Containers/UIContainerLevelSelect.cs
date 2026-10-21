using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008c8; bodies translated from the 1.10.1 x86-64 binary.
    // The level select screen: follows the zone and level picked in the zones container and starts the selected level.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerLevelSelect : UIContainer
    {
        [SerializeField] private UIModernEvent m_exitEvent;
        [SerializeField] private UIContainerLevelSelectZones m_zonesContainer;
        [Tooltip("Optional alert badge for displaying count of unlocked unseen ornaments.")]
        [SerializeField]
        private UIWidgetSDTAlertBadge m_ornamentsAlertBadge;
        public readonly Bindable<Strings> SelectedZoneTitle = new Bindable<Strings>();
        public readonly Bindable<Strings> SelectedZoneSubtitle = new Bindable<Strings>();
        public readonly Bindable<Strings> SelectedLevelName = new Bindable<Strings>();
        public readonly Bindable<bool> HasNewOrnaments = new Bindable<bool>();
        public readonly Bindable<int> CollectedOrnaments = new Bindable<int>();
        public readonly Bindable<int> TotalOrnaments = new Bindable<int>();
        public readonly Bindable<AssetReferenceT<Sprite>> ZoneIconAsset = new Bindable<AssetReferenceT<Sprite>>();
        public readonly Bindable<Color> ZoneBackgroundStartColour = new Bindable<Color>();
        public readonly Bindable<Color> ZoneBackgroundEndColour = new Bindable<Color>();
        public readonly Bindable<bool> RewardsAvailable = new Bindable<bool>();
        private App m_app;
        private GameplayLevelDefinition m_selectedLevelDefinition;
        private AsyncOperationHandle m_zoneIconAssetHandle;

        // 0x060032bd (0x637FD0): hands the zones container the selection callbacks, follows the ornament counts and shows the
        // reward tracks once their feature is unlocked.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>(null, true);
            m_zonesContainer.Setup(new UIContainerLevelSelectZonesParameters(UpdateLevelState, OnZoneSelected));
            ProcessManager.GetSystemRef<OrnamentManager>(null, true).InvokeOnValid(UpdateOrnamentBinders);
            ChallengeManager challengeManager = ProcessManager.GetSystem<ChallengeManager>(null, true);
            challengeManager.RefreshRewardTracksFeatureUnlocked();
            RewardsAvailable.Value = challengeManager.RewardTracksFeatureUnlocked != SaveDataChallengeRewardTrack.FeatureUnlockState.Locked;
        }

        // 0x060032be (0x638280)
        public void Action_Continue()
        {
            Continue();
        }

        // 0x060032bf (0x638350)
        private void OnZoneSelected(GameplayLevelCategory zone)
        {
            SelectedZoneTitle.Value = zone.Title;
            SelectedZoneSubtitle.Value = zone.Subtitle;
            ZoneIconAsset.Value = zone.IconAsset;
            ZoneBackgroundStartColour.Value = zone.BackgroundStartColour;
            ZoneBackgroundEndColour.Value = zone.BackgroundEndColour;
        }

        // 0x060032c0 (0x6383F0): the same bindings as OnZoneSelected.
        private void UpdateZoneState(GameplayLevelCategory zone)
        {
            SelectedZoneTitle.Value = zone.Title;
            SelectedZoneSubtitle.Value = zone.Subtitle;
            ZoneIconAsset.Value = zone.IconAsset;
            ZoneBackgroundStartColour.Value = zone.BackgroundStartColour;
            ZoneBackgroundEndColour.Value = zone.BackgroundEndColour;
        }

        // 0x060032c1 (0x638490): records the level as the meta-game state, keeping the mission of any state already stored.
        private void UpdateLevelState(GameplayLevelDefinition levelDefinition)
        {
            MissionDefinition missionDefinition = null;
            if (m_app.Storage.HasValue(AppFSMKeys.MetaGameState))
                missionDefinition = m_app.Storage.GetValue<MetaGameState>(AppFSMKeys.MetaGameState, null, true).MissionDefinition;
            m_app.Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelDefinition, missionDefinition));
            SelectedLevelName.Value = levelDefinition.DisplayName;
            m_selectedLevelDefinition = levelDefinition;
        }

        // 0x060032c2 (0x638290)
        private void Continue()
        {
            ProcessManager.GetSystem<LevelManager>(null, true).SetupFsmVarsToLoadLevel(m_selectedLevelDefinition);
            App app = ProcessManager.GetSystem<App>(null, true);
            app.SetLastLevelUISelectedGUID(m_selectedLevelDefinition, true);
            app.AddUniqueUIModernEvent(m_exitEvent);
        }

        // 0x060032c3 (0x638720): counts the unlocked, unseen, collected ornaments across every collection for the alert badge.
        private void UpdateOrnamentBinders(OrnamentManager ornamentManager)
        {
            CollectedOrnaments.Value = ornamentManager.TotalCollected;
            TotalOrnaments.Value = ornamentManager.TotalOrnaments;
            HasNewOrnaments.Value = false;
            int newOrnamentCount = 0;
            foreach (KeyValuePair<OrnamentCategory, Dictionary<OrnamentIdentifier, OrnamentManager.OrnamentStatus>> collection in ornamentManager.OrnamentCollections)
            {
                foreach (KeyValuePair<OrnamentIdentifier, OrnamentManager.OrnamentStatus> ornament in collection.Value)
                {
                    OrnamentManager.OrnamentStatus status = ornament.Value;
                    if (status.Unlocked && !status.Seen && status.Collected)
                        newOrnamentCount++;
                }
            }
            HasNewOrnaments.Value = newOrnamentCount > 0;
            if (m_ornamentsAlertBadge != null)
                m_ornamentsAlertBadge.SetAlert(false, newOrnamentCount);
        }

        // 0x060032c4
        public UIContainerLevelSelect() { }
    }
}
