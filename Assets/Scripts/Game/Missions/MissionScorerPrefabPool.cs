using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000787: pools the scorer prefabs of an instanced tracker's objectives of one type and, on
    // a refresh interval taken from the active performance profile, spawns the ones near the character.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MissionScorerPrefabPool : PrefabPool<MissionScorerInstance>
    {
        [Serializable]
        private struct QualityAttributes
        {
            public PerformanceProfile.QualityLevel QualityLevel;
            [Tooltip("If one of the instances is less than this distance (in meters), then it will be activated in the pool.")]
            [Min(0f)]
            public float DistanceMeters;
            [Tooltip("How long to wait until we refresh the prefabs in the pool in seconds.")]
            [Min(0f)]
            public float RefreshRateSeconds;
        }

        [SerializeField]
        [Tooltip("The instanced tracker to read objectives from.")]
        private MissionInstancedTracker m_instancedTracker;
        [SerializeField]
        [Tooltip("Filters the objectives down to only pooling of these type. This must be done because using addressables we can't compare prefabs.")]
        private MissionScorerObjectiveType m_type;
        [SerializeField] private ScalableFeature m_scalableFeature;
        [SerializeField] private List<QualityAttributes> m_qualitySettings;
        private readonly SystemRef<ScalablePerformance> m_scalablePerformanceRef = ProcessManager.GetSystemRef<ScalablePerformance>(null, true);
        private readonly HashSet<MissionScorerObjective> m_objectiveKeys = new HashSet<MissionScorerObjective>();
        private readonly HashSet<MissionScorerObjective> m_activeObjectives = new HashSet<MissionScorerObjective>();
        private readonly Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>> m_instancesByObjective =
            new Dictionary<MissionScorerObjective, PooledPrefab<MissionScorerInstance>>();
        private CharacterManager m_characterManager;
        private WaitForSeconds m_refreshWaitForSeconds;
        private float m_distanceSqr;
        private float m_renderDistanceMeters;
        private float m_renderRefreshRateSeconds;

        // 06002a39 (0x5D6B00): the render settings are read before the base pool builds its instances.
        protected override void Awake()
        {
            SetRenderSettings();
            base.Awake();
        }

        // 06002a3a (0x5D6D40): every objective of the pooled type gets a pool entry with no instance yet (index -1).
        private void Start()
        {
            foreach (MissionObjective objective in m_instancedTracker.MissionObjectives)
            {
                if (objective is MissionScorerObjective scorerObjective && scorerObjective.Type == m_type)
                    m_instancesByObjective.Add(scorerObjective, new PooledPrefab<MissionScorerInstance>(null, -1));
            }
            m_objectiveKeys.Clear();
            m_objectiveKeys.AddRange(m_instancesByObjective.Keys);
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(OnCharacterManagerValid);
        }

        // 06002a3b (0x5D6B50): the last quality entry matching the active profile's level supplies the distance and
        // refresh rate. A pool with neither a distance nor a refresh rate is allowed to resize.
        private void SetRenderSettings()
        {
            PerformanceAttribute attribute = m_scalablePerformanceRef.Get().ActiveProfile.GetAttribute(m_scalableFeature);
            foreach (QualityAttributes quality in m_qualitySettings)
            {
                if (attribute.Level != quality.QualityLevel) continue;
                m_renderDistanceMeters = quality.DistanceMeters;
                m_renderRefreshRateSeconds = quality.RefreshRateSeconds;
            }
            m_refreshWaitForSeconds = new WaitForSeconds(m_renderRefreshRateSeconds);
            m_distanceSqr = m_renderDistanceMeters * m_renderDistanceMeters;
            if (m_distanceSqr <= 0f && m_renderRefreshRateSeconds <= 0f)
                m_resizeWhenFull = true;
        }

        // 06002a3c (0x5D71E0)
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            StartCoroutine(RefreshPooledPrefabs());
        }

        // 06002a3d (0x5D7260) and its state machine (0x5D7520): each refresh, an incomplete objective whose position is
        // within range of the character gets an instance spawned at its transform, and one that has left range is
        // despawned.
        private IEnumerator RefreshPooledPrefabs()
        {
            while (true)
            {
                if (!m_characterManager.TryGetCurrentCharacter(out Character character))
                {
                    yield return m_refreshWaitForSeconds;
                    continue;
                }
                foreach (MissionScorerObjective objective in m_objectiveKeys)
                {
                    if (objective.IsComplete) continue;
                    PooledPrefab<MissionScorerInstance> pooledInstance = m_instancesByObjective[objective];
                    bool isActive = m_activeObjectives.Contains(objective);
                    bool withinDistance = IsWithinDistance(objective, character);
                    if (!isActive && withinDistance)
                    {
                        PooledPrefab<MissionScorerInstance> spawned = SpawnInstance(objective.transform.position,
                            objective.transform.rotation, objective.transform.localScale, 1f, null);
                        if (spawned.Instance == null) continue;
                        spawned.Instance.Objective = objective;
                        spawned.Instance.Objective.ScorerEvaluator = spawned.Instance.Evaluator;
                        spawned.Instance.gameObject.SetActive(true);
                        m_instancesByObjective[objective] = spawned;
                        m_activeObjectives.Add(objective);
                    }
                    else if (isActive && !withinDistance)
                    {
                        m_activeObjectives.Remove(objective);
                        DespawnInstance(in pooledInstance);
                    }
                }
                yield return m_refreshWaitForSeconds;
            }
        }

        // 06002a3e (0x5D72F0): without a configured distance every objective counts as in range.
        private bool IsWithinDistance(MissionScorerObjective scorerObjective, Character currentCharacter)
        {
            if (m_distanceSqr <= 0f) return true;
            Vector3 offset = scorerObjective.transform.position - currentCharacter.transform.position;
            return m_distanceSqr > offset.sqrMagnitude;
        }

        public MissionScorerPrefabPool() { }
    }
}
