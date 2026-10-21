using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008c9; bodies translated from the 1.10.1 x86-64 binary.
    // One zone page of the level select: a level widget per level, set up from the player's progression and save data.
    // The compiler-generated closure names (<>c__DisplayClass12_n) follow member order, with HasAnyNewContent declared between the serialized fields and the cached managers.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerLevelSelectZone : UIContainer
    {
        [SerializeField] private UIWidgetForGameplayLevelDefinition[] m_widgetForGameplayLevelDefinitions = Array.Empty<UIWidgetForGameplayLevelDefinition>();
        [SerializeField] private int m_levelNumericalNameOffset = 1;

        // 0x060032c5 / 0x060032c6
        public bool HasAnyNewContent { get; private set; }

        private UIContainerLevelSelectZoneParameters m_parameters;
        private ProgressionManager m_progressionManager;
        private SaveManager m_saveManager;
        private readonly SystemRef<ProgressionManager> m_progressionManagerRef = ProcessManager.GetSystemRef<ProgressionManager>(null, true);

        // 0x060032c7 (0x638F00)
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerLevelSelectZoneParameters>();
            m_progressionManagerRef.InvokeOnValid(OnProgressionManagerValid);
        }

        // 0x060032c8 (0x638FD0): every level widget but the selected one is deselected without notifying.
        public void UpdateDisabledLevelWidgets(GameplayLevelDefinition selected)
        {
            for (int i = 0; i < m_widgetForGameplayLevelDefinitions.Length; i++)
            {
                UIWidgetForGameplayLevelDefinition widgetForLevel = m_widgetForGameplayLevelDefinitions[i];
                if (selected == widgetForLevel.LevelDefinition)
                    continue;
                widgetForLevel.Widget.DeselectWithoutNotify();
            }
        }

        // 0x060032c9 (0x639030): the listener reports each level widget's toggle together with that widget's level.
        public void AddValueChangeListener(Action<bool, GameplayLevelDefinition> onValueChanged)
        {
            foreach (UIWidgetForGameplayLevelDefinition widgetForLevel in m_widgetForGameplayLevelDefinitions)
                widgetForLevel.Widget.AddValueChangeListener(value => onValueChanged(value, widgetForLevel.LevelDefinition));
        }

        // 0x060032ca (0x639190)
        public void ToggleOn(bool on)
        {
            for (int i = 0; i < m_widgetForGameplayLevelDefinitions.Length; i++)
                m_widgetForGameplayLevelDefinitions[i].Widget.ToggleWidgetNavigation(on);
            if (on)
                TrySelectBestLevel();
        }

        // 0x060032cb (0x639210): selects the preselected level when it is unlocked, otherwise the last unlocked level.
        private void TrySelectBestLevel()
        {
            UIWidgetSDTLevel bestLevel = null;
            for (int i = 0; i < m_widgetForGameplayLevelDefinitions.Length; i++)
            {
                UIWidgetForGameplayLevelDefinition widgetForLevel = m_widgetForGameplayLevelDefinitions[i];
                UIWidgetSDTLevel levelWidget = widgetForLevel.Widget;
                if (levelWidget.IsLocked.Value)
                    continue;
                if (m_parameters.PreselectedLevel == widgetForLevel.LevelDefinition)
                {
                    levelWidget.Select();
                    return;
                }
                bestLevel = levelWidget;
            }
            if (bestLevel != null)
                bestLevel.Select();
        }

        // 0x060032cc (0x639310)
        private void OnProgressionManagerValid(ProgressionManager progressionManager)
        {
            m_progressionManager = progressionManager;
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 0x060032cd (0x639400)
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            SetupLevels();
        }

        // 0x060032ce (0x639430): sets up every level widget. A level that is not locked and has not had its unlock seen is marked
        // seen in the save data once its widget is set up.
        private void SetupLevels()
        {
            IReadOnlyDictionary<CollectableType, ProgressionCollectable> overallProgress = m_progressionManager.GetTotalProgress();
            for (int i = 0; i < m_widgetForGameplayLevelDefinitions.Length; i++)
            {
                UIWidgetForGameplayLevelDefinition widgetForLevel = m_widgetForGameplayLevelDefinitions[i];
                GameplayLevelDefinition levelDefinition = widgetForLevel.LevelDefinition;
                UIWidgetSDTLevel levelWidget = widgetForLevel.Widget;
                ProgressionCollectableLevel levelProgress = m_progressionManager.GetLevelProgress(levelDefinition);
                SaveDataLevel levelSave = m_saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
                bool alwaysUnlocked = levelDefinition.AlwaysUnlocked;
                bool isNextLevel = m_parameters.NextProductionLevelLocked == levelDefinition;
                bool locked;
                bool shouldShow;
                bool unlockSeen;
                if (alwaysUnlocked)
                {
                    locked = false;
                    shouldShow = levelDefinition.ShouldShowInLevelSelect();
                    unlockSeen = true;
                }
                else
                {
                    locked = !levelDefinition.MeetsAllRequirements();
                    shouldShow = levelDefinition.ShouldShowInLevelSelect();
                    unlockSeen = levelSave.LevelSelectUnlockSeen;
                }
                IReadOnlyList<string> missionGUIDs = Array.Empty<string>();
                if (levelDefinition.MissionList != null)
                    missionGUIDs = levelDefinition.MissionList.GetMissionGUIDs();
                bool hidden = !shouldShow;
                int levelNumericalName = m_levelNumericalNameOffset + i;
                if (locked)
                {
                    levelWidget.Setup(new UIWidgetSDTLevelParameters(levelDefinition, levelProgress, overallProgress, isNextLevel, true, unlockSeen, false, levelNumericalName, hidden));
                    continue;
                }
                bool hasAnyNewContent = levelSave.HasAnyNewContent(missionGUIDs);
                HasAnyNewContent = HasAnyNewContent | hasAnyNewContent;
                levelWidget.Setup(new UIWidgetSDTLevelParameters(levelDefinition, levelProgress, overallProgress, isNextLevel, false, unlockSeen, hasAnyNewContent, levelNumericalName, hidden));
                if (unlockSeen)
                    continue;
                levelSave.LevelSelectUnlockSeen = true;
                levelSave.UnlockSeen = true;
                m_saveManager.RequestSave();
            }
        }

        // 0x060032cf
        public UIContainerLevelSelectZone() { }
    }
}
