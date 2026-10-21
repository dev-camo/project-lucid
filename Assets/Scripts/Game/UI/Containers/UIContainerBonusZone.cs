using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000879; bodies translated from the 1.10.1 x86-64 binary.
    // The bonus zone screen: the zone icon, a reveal animation for the first visit and one level
    // widget per level of the zone.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerBonusZone : UIContainer
    {
        [SerializeField] private GameplayLevelCategory m_bonusZoneCategory;
        [SerializeField] private GameplayLevelDefinition m_revealLevelDefinition;
        [SerializeField] private GameplayLevelDefinition[] m_levelsWaitForReveal;
        [SerializeField] private UIWidgetForGameplayLevelDefinition[] m_widgetToLevelPairs;
        [SerializeField] private UIWidgetAddressableSprite m_zoneIcon;
        [SerializeField] private int m_levelNumericalNameOffset = 1;
        [SerializeField] private UIModernEvent m_continueEvent;
        [SerializeField] private Animation m_revealAnimation;
        public readonly Bindable<bool> ShowReveal = new Bindable<bool>();
        private App m_app;
        private LevelManager m_levelManager;
        private SaveManager m_saveManager;
        private ProgressionManager m_progressionManager;
        private GameplayLevelDefinition m_selectedLevelDefinition;
        private bool m_revealIsDoneAnimating;

        // 0x06003100 (0x619A70)
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>(null, true);
            m_levelManager = ProcessManager.GetSystem<LevelManager>(null, true);
            m_saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            m_progressionManager = ProcessManager.GetSystem<ProgressionManager>(null, true);
            SetupZone();
            SetupLevels();
        }

        // 0x06003101 (0x61A320): the native body is an inlined copy of Continue.
        public void Action_Continue()
        {
            Continue();
        }

        // 0x06003102 (0x61A3E0): the levels waiting for the reveal are set up again once it is done.
        public void Action_FinishRevealAnimation()
        {
            m_revealIsDoneAnimating = true;
            SetupLevels();
        }

        // 0x06003103 (0x61A380)
        private void Continue()
        {
            m_levelManager.SetupFsmVarsToLoadLevel(m_selectedLevelDefinition, null);
            m_app.SetLastLevelUISelectedGUID(m_selectedLevelDefinition, true);
            m_app.AddUniqueUIModernEvent(m_continueEvent);
        }

        // 0x06003104 (0x619BB0): shows the zone icon and, the first time, records that the zone
        // of the first level widget has had its unlock seen.
        private void SetupZone()
        {
            m_zoneIcon.SetSpriteReference(m_bonusZoneCategory.IconAsset);
            SaveDataGame currentSave = m_saveManager.CurrentSave;
            if (!m_levelManager.GameLevels.TryGetZoneForLevel(m_widgetToLevelPairs[0].LevelDefinition, out GameplayLevelCategory zone))
                return;
            if (currentSave.ZoneUnlockSeenGuids.Contains(zone.GetGUID()))
                return;
            currentSave.AddZoneUnlockSeenGuid(zone.GetGUID());
            m_saveManager.RequestSave();
        }

        // 0x06003105 (0x619C80): configures each level widget from the progression and save data.
        // Levels that wait for the reveal are shown as unseen once the reveal has finished animating.
        private void SetupLevels()
        {
            SaveDataGame currentSave = m_saveManager.CurrentSave;
            GameplayLevelDefinition firstLockedLevel = m_levelManager.GetFirstProductionLevelLocked(m_bonusZoneCategory);
            GameplayLevelDefinition levelToSelect = m_levelManager.FindMetaGameLevel();
            SaveDataLevel revealLevelData = currentSave.GetOrCreateLevelData(m_revealLevelDefinition.GetGUID());
            bool revealUnlocked = m_revealLevelDefinition.MeetsAllRequirements();
            bool revealNotSeen = !revealLevelData.LevelSelectUnlockSeen;
            ShowReveal.Value = !revealUnlocked || revealNotSeen;
            if (!m_bonusZoneCategory.Contains(levelToSelect))
            {
                // The meta game level is outside this zone: select the zone's first level instead.
                foreach (GameplayLevelUIEntry entry in m_bonusZoneCategory.GameplayEntries)
                {
                    if (entry is GameplayLevelDefinition levelEntry)
                    {
                        levelToSelect = levelEntry;
                        break;
                    }
                }
            }
            bool playReveal = revealUnlocked && revealNotSeen;
            IReadOnlyDictionary<CollectableType, ProgressionCollectable> totalProgress = m_progressionManager.GetTotalProgress();
            for (int i = 0; i < m_widgetToLevelPairs.Length; i++)
            {
                GameplayLevelDefinition levelDefinition = m_widgetToLevelPairs[i].LevelDefinition;
                UIWidgetSDTLevel widget = m_widgetToLevelPairs[i].Widget;
                bool alwaysUnlocked = levelDefinition.AlwaysUnlocked;
                bool isNextLevel = firstLockedLevel == levelDefinition;
                bool locked = false;
                if (!alwaysUnlocked)
                    locked = !levelDefinition.MeetsAllRequirements();
                bool showInLevelSelect = levelDefinition.ShouldShowInLevelSelect();
                ProgressionCollectableLevel levelProgress = m_progressionManager.GetLevelProgress(levelDefinition);
                SaveDataLevel levelData = currentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
                bool unlockSeen = alwaysUnlocked || levelData.LevelSelectUnlockSeen;
                IReadOnlyList<string> missionGuids = Array.Empty<string>();
                if (levelDefinition.MissionList != null)
                    missionGuids = levelDefinition.MissionList.GetMissionGUIDs();
                bool hidden = !showInLevelSelect;
                bool isNew = !locked && levelData.HasAnyNewContent(missionGuids);
                int levelNumericalName = m_levelNumericalNameOffset + i;
                bool waitsForReveal = m_levelsWaitForReveal.Contains(levelDefinition) && m_revealIsDoneAnimating;
                unlockSeen &= !waitsForReveal;
                widget.Setup(new UIWidgetSDTLevelParameters(levelDefinition, levelProgress, totalProgress, isNextLevel, locked, unlockSeen, isNew, levelNumericalName, hidden));
                widget.AddValueChangeListener(isOn => SelectLevelDefinition(isOn, levelDefinition));
                if (levelToSelect == levelDefinition)
                    widget.Select(true);
                if (!waitsForReveal && !locked && !unlockSeen)
                {
                    levelData.LevelSelectUnlockSeen = true;
                    levelData.UnlockSeen = true;
                    m_saveManager.RequestSave();
                }
            }
            if (playReveal)
            {
                revealLevelData.LevelSelectUnlockSeen = true;
                revealLevelData.UnlockSeen = true;
                m_saveManager.RequestSave();
                m_revealAnimation.Play();
            }
        }

        // 0x06003106 (0x61A410): only a widget turning on changes the selection.
        private void SelectLevelDefinition(bool isOn, GameplayLevelDefinition levelDefinition)
        {
            if (isOn)
                m_selectedLevelDefinition = levelDefinition;
        }

        // 0x06003107
        public UIContainerBonusZone() { }
    }
}
