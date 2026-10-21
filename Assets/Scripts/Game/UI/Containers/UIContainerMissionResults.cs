using System.Collections.Generic;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e5; bodies translated from the 1.10.1 x86-64 binary.
    // Spawns one instanced-mission results container per valid (sub)mission of the finished mission.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerMissionResults : UIContainer
    {
        [SerializeField] private RectTransform m_root;
        [SerializeField] private bool m_persistentCompletion;
        [SerializeField] private bool m_useShortResultsContainer;
        [SerializeField] private bool m_animateMissionProgressDelta;
        [SerializeField] private List<CollectableType> m_rewardsToIgnore = new List<CollectableType>();
        public readonly Bindable<bool> Show = new Bindable<bool>();
        private UIContainerMissionResultsParameters m_parameters;
        private readonly List<UIContainerIdentifier> m_instancedUIContainers = new List<UIContainerIdentifier>();

        // 06003356 (0x63FDD0): clears the children of the root (the index advances while the destroys are still pending)
        // and builds the instanced containers once the save manager is valid.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerMissionResultsParameters>();
            for (int i = 0; i < m_root.childCount; i++)
                Destroy(m_root.GetChild(i).gameObject);
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 06003357 (0x63FF70)
        public bool CanShow()
        {
            return Show.Value;
        }

        // 06003358 (0x63FF90)
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            List<MissionDefinition> missions = GetMissions(m_parameters.MissionDefinition);
            foreach (MissionDefinition mission in missions)
                InstantiateInstancedTrackerPrefab(m_parameters.LevelDefinition, mission);
            Show.Value = missions.Count > 0;
        }

        // 06003359 (0x640100): the valid sub-missions come first, then the mission itself.
        private List<MissionDefinition> GetMissions(MissionDefinition missionDefinition)
        {
            List<MissionDefinition> missions = new List<MissionDefinition>();
            foreach (MissionDefinition subMission in missionDefinition.SubMissions)
            {
                if (IsValidMission(subMission))
                    missions.Add(subMission);
            }
            if (IsValidMission(missionDefinition))
                missions.Add(missionDefinition);
            return missions;
        }

        // 0600335a (0x640670)
        private bool IsValidMission(MissionDefinition missionDefinition)
        {
            if (!missionDefinition.ShowProgressInUI)
                return false;
            if (!missionDefinition.IsInstancedTracker)
                return false;
            if (m_persistentCompletion && !missionDefinition.IsPersistentTracker)
                return false;
            UIContainerIdentifier resultsScreenIdentifier = m_useShortResultsContainer
                ? missionDefinition.ShortResultsScreenIdentifier
                : missionDefinition.ResultsScreenIdentifier;
            if (missionDefinition.TargetObjectivesToComplete <= 0)
                return false;
            if (resultsScreenIdentifier == null)
                return false;
            return !m_rewardsToIgnore.Contains(missionDefinition.RewardType);
        }

        // 0600335b (0x640370): the previous progress comes from the data cached before the level, and is 0 without it.
        private void InstantiateInstancedTrackerPrefab(GameplayLevelDefinition levelDefinition, MissionDefinition mission)
        {
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            SaveDataLevelMission saveData = missionManager.GetSaveDataForMission(mission.GetGUID(), levelDefinition.GetGUID(), null);
            UIContainerIdentifier resultsScreenIdentifier = m_useShortResultsContainer
                ? mission.ShortResultsScreenIdentifier
                : mission.ResultsScreenIdentifier;
            UIContainer prefab = resultsScreenIdentifier.GetOrCreateContainer();
            m_instancedUIContainers.Add(resultsScreenIdentifier);
            UIContainer container = Instantiate(prefab, m_root);
            int previousProgress = 0;
            IReadOnlyList<int> previouslyCompletedObjectiveIndices = null;
            if (missionManager.PreLevelCachedMissionData.TryGetValue(mission.GetGUID(), out SaveDataLevelMission cachedSaveData))
            {
                previousProgress = cachedSaveData.Progress;
                previouslyCompletedObjectiveIndices = cachedSaveData.CompletedObjectiveIndices;
            }
            container.Setup(new UIContainerInstancedMissionProgressParameters(
                mission, saveData.Progress, previousProgress, m_animateMissionProgressDelta,
                previouslyCompletedObjectiveIndices, saveData.CompletedObjectiveIndices));
        }

        // 0600335c (0x640750)
        private void OnDestroy()
        {
            foreach (UIContainerIdentifier instancedUIContainer in m_instancedUIContainers)
                instancedUIContainer.Close();
        }

        // 0600335d
        public UIContainerMissionResults() { }
    }
}
