using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008c0; bodies translated from the 1.10.1 x86-64 binary.
    // The in-game HUD host for the progress widgets of the instanced-tracker missions: one UIContainerMissionProgress per tracker,
    // grouped per mission type when the tracker names a group container.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerGameHUDMissionProgress : UIContainer
    {
        [SerializeField] private UIContainerMissionProgress m_defaultActiveMissionProgressPrefab;
        [SerializeField] private UIContainerMissionProgress m_defaultPersistentMissionProgressPrefab;
        [SerializeField]
        [FormerlySerializedAs("m_containerParent")]
        private Transform m_persistentMissionProgressParent;
        [SerializeField] private Transform m_defaultMissionProgressParent;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly Dictionary<MissionDefinition, UIContainerMissionProgress> m_activeMissionContainersByDefinition = new Dictionary<MissionDefinition, UIContainerMissionProgress>();

        // 0x06003286 (0x633F90)
        private void Start()
        {
            m_missionManagerRef.InvokeOnValid(InitialiseMissionUI);
        }

        // 0x06003287 (0x634020)
        private void OnDestroy()
        {
            if (m_missionManagerRef.IsNull())
                return;
            CloseMissionUI();
            m_missionManagerRef.Get().OnMissionsReset -= OnMissionsReset;
        }

        // 0x06003288 (0x634590)
        private void InitialiseMissionUI(MissionManager missionManager)
        {
            missionManager.OnMissionsReset += OnMissionsReset;
            SetupMissionUI(missionManager);
        }

        // 0x06003289 (0x6340F0): unhooks every instanced tracker and the island event, then forgets the spawned containers.
        private void CloseMissionUI()
        {
            MissionManager missionManager = m_missionManagerRef.Get();
            foreach ((string _, MissionState missionState) in missionManager.MissionStates)
            {
                if (!(missionState.Tracker is MissionInstancedTracker tracker))
                    continue;
                tracker.OnStatusChanged -= RefreshActiveMission;
            }
            missionManager.OnMissionIslandActive -= OnMissionIslandActive;
            m_activeMissionContainersByDefinition.Clear();
        }

        // 0x0600328a (0x634840)
        private void OnMissionsReset(bool subMissionsReloaded)
        {
            if (subMissionsReloaded)
            {
                CloseMissionUI();
                SetupMissionUI(m_missionManagerRef.Get());
            }
        }

        // 0x0600328b (0x634620): the island event is hooked once, for the first container that only shows while its island is active.
        private void SetupMissionUI(MissionManager missionManager)
        {
            SetupActiveMissionsUI();
            foreach (KeyValuePair<MissionDefinition, UIContainerMissionProgress> pair in m_activeMissionContainersByDefinition)
            {
                UIContainerMissionProgress container = pair.Value;
                if (!container.OnlyShowWhenMissionIslandActive)
                    continue;
                missionManager.OnMissionIslandActive += OnMissionIslandActive;
                if (missionManager.TryGetIslandOverrideMissionState(out MissionState islandOverrideMissionState))
                    OnMissionIslandActive(islandOverrideMissionState);
                break;
            }
        }

        // 0x0600328c (0x6348A0): spawns a progress container for every completable instanced tracker, under the persistent or default
        // parent, inside a per-mission-type group container when the tracker names one. A container is left hidden unless it is always
        // displayed and not restricted to its island.
        private void SetupActiveMissionsUI()
        {
            var groupContainersByType = new Dictionary<MissionType, UIContainer>();
            foreach ((string _, MissionState missionState) in m_missionManagerRef.Get().MissionStates)
            {
                if (!(missionState.Tracker is MissionInstancedTracker tracker))
                    continue;
                if (!tracker.CanComplete)
                    continue;
                bool isPersistent = missionState.Definition.IsPersistentTracker;
                UIContainerIdentifier identifierOverride = tracker.UIContainerIdentifierOverride;
                UIContainerMissionProgress prefab = isPersistent
                    ? GetPersistentContainerPrefab(identifierOverride)
                    : GetActiveContainerPrefab(identifierOverride);
                Transform parent = isPersistent ? m_persistentMissionProgressParent : m_defaultMissionProgressParent;
                if (tracker.UIContainerIdentifierGroup != null)
                {
                    MissionType missionType = tracker.Definition.Type;
                    if (!groupContainersByType.TryGetValue(missionType, out UIContainer groupContainer))
                    {
                        groupContainer = Object.Instantiate(GetContainerPrefab(tracker.UIContainerIdentifierGroup), parent);
                        groupContainersByType[missionType] = groupContainer;
                    }
                    parent = groupContainer.transform;
                }
                UIContainerMissionProgress container = Object.Instantiate(prefab, parent);
                container.Setup(new UIContainerMissionProgressParameters(tracker));
                if (!container.AlwaysDisplay || container.OnlyShowWhenMissionIslandActive)
                    container.gameObject.SetActive(false);
                m_activeMissionContainersByDefinition.Add(tracker.Definition, container);
                tracker.OnStatusChanged += RefreshActiveMission;
            }
        }

        // 0x0600328d (0x635080): the island-bound containers are shown only for the tracker of the active island mission.
        private void OnMissionIslandActive(MissionState missionState)
        {
            foreach (KeyValuePair<MissionDefinition, UIContainerMissionProgress> pair in m_activeMissionContainersByDefinition)
            {
                UIContainerMissionProgress container = pair.Value;
                if (!container.OnlyShowWhenMissionIslandActive)
                    continue;
                container.gameObject.SetActive(container.InstancedTracker == missionState.Tracker);
            }
        }

        // 0x0600328e (0x6353E0): a tracker without a group refreshes its own container; a grouped one refreshes every container of its mission type.
        private void RefreshActiveMission(MissionInstancedTracker tracker)
        {
            if (tracker.UIContainerIdentifierGroup == null)
            {
                if (m_activeMissionContainersByDefinition.TryGetValue(tracker.Definition, out UIContainerMissionProgress container))
                    container.Refresh();
            }
            else
            {
                MissionType missionType = tracker.Definition.Type;
                foreach (KeyValuePair<MissionDefinition, UIContainerMissionProgress> pair in m_activeMissionContainersByDefinition)
                {
                    if (pair.Key.Type == missionType)
                        pair.Value.Refresh();
                }
            }
        }

        // 0x0600328f (0x635270)
        private UIContainerMissionProgress GetActiveContainerPrefab(UIContainerIdentifier missionProgressIdentifier)
        {
            if (missionProgressIdentifier == null)
                return m_defaultActiveMissionProgressPrefab;
            return GetContainerPrefab(missionProgressIdentifier);
        }

        // 0x06003290 (0x6352E0)
        private UIContainerMissionProgress GetPersistentContainerPrefab(UIContainerIdentifier missionProgressIdentifier)
        {
            if (missionProgressIdentifier == null)
                return m_defaultPersistentMissionProgressPrefab;
            return GetContainerPrefab(missionProgressIdentifier);
        }

        // 0x06003291 (0x635350)
        private UIContainerMissionProgress GetContainerPrefab(UIContainerIdentifier missionProgressIdentifier)
        {
            return missionProgressIdentifier.Prefab as UIContainerMissionProgress;
        }

        // 0x06003292
        public UIContainerGameHUDMissionProgress() { }
    }
}
