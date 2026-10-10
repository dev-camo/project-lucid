using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Whole original Game.Runtime 020001f7; original ordinary methods 06000b6a..b8c.
    // The two original instance callbacks and five <>c methods are represented by
    // genuine C# lambdas. Their compiler ordinals/binding are separate emission holds.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossManager : MonoBehaviour, ISystem, ISaveGameListener
    {
        [SerializeField] private UIContainerIdentifier m_HUDContainer;
        [SerializeField] private UIContainerIdentifier m_onScreenControlsContainer;
        private App m_app;
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly List<Boss> m_bosses = new List<Boss>();
        private Dictionary<LevelSetupTypes, GameplayLevelDefinition> m_bossesDefeatedByLevelType = new Dictionary<LevelSetupTypes, GameplayLevelDefinition>();
        private readonly Dictionary<PrefabPoolType, BossTimedObjectPool> m_bossWorldSpacePools = new Dictionary<PrefabPoolType, BossTimedObjectPool>();
        private FSMStateLoader m_fsmStateLoader;
        private bool m_statesLoaded;
        private int m_sharedHealthTotal;
        private int m_sharedHealthCurrent;
        private Strings m_bossEncounterName = Strings.NONE;
        public Action<int> OnBossHealthPoolChange;
        public Action<int> OnBossHealthTotalUpdate;
        // 06000b91..b93 are genuine empty constructor lambdas in both architectures.
        public Action OnBossDefeated = () => { };
        public Action OnBossFullyDefeated = () => { };
        public Action OnBossHardFailure = () => { };
        private int m_activeBossIndex;
        private ProjectileManager m_projectileManager;
        private bool m_bossContinuePhase;
        private bool m_bossHitThisFrame;
        private Boss m_lastDeregisteredBoss;
        private UIManager m_uiManager;

        // 06000b6a..b6e: the list is an original live alias.
        public List<Boss> Bosses => m_bosses;
        public bool BossManagerActive => m_bosses.Count > 0;
        public int BossHealthTotal => m_sharedHealthTotal;
        public int BossHealthCurrent => m_sharedHealthCurrent;
        public Strings BossName => m_bossEncounterName;

        // 06000b6f: App readiness is subscribed before system registration.
        private void Awake()
        {
            ProcessManager.GetSystemRef<App>(null, true).InvokeOnValid(OnAppReady);
            ProcessManager.RegisterSystem(this, null, false, false);
        }

        // 06000b70: retain assignment before the DataManager subscription.
        private void OnAppReady(App app)
        {
            m_app = app;
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(OnDataReady);
        }

        // 06000b71: copy the original BossStateMachines dictionary before Values;
        // the supplied parameter is ignored and each later field access is live.
        private void OnDataReady(DataManager _)
        {
            var stateMachines = new Dictionary<string, FiniteStateMachineScriptableObject>(m_app.DataManager.BossStateMachines).Values;
            m_fsmStateLoader = new FSMStateLoader(stateMachines, OnStatesLoaded);
            m_fsmStateLoader.LoadStates();
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, OnShutdown);
            ProcessManager.GetSystemRef<ProjectileManager>(null, true).InvokeOnValid(manager => m_projectileManager = manager);
            m_saveManagerRef.InvokeOnValid(RegisterSaveManager);
            ProcessManager.GetSystemRef<UIManager>(null, true).InvokeOnValid(manager => m_uiManager = manager);
        }

        // 06000b72: immediate listener notification remains enabled.
        private void RegisterSaveManager(SaveManager saveManager) => saveManager.AddDependantListener(this, true);

        // 06000b73: registration always appends and updates the 32-bit shared pool.
        // The native bodies combine the health field operations; exceptional null
        // store scheduling differs between architectures under disabled checks.
        public void Register(Boss boss)
        {
            m_bosses.Add(boss);
            unchecked
            {
                m_sharedHealthTotal += boss.Definition.Traits.HealthPool;
                m_sharedHealthCurrent += boss.CurrentHealth;
            }
            if (m_statesLoaded) boss.InitialiseBoss();
            m_bossEncounterName = boss.Definition.Name;
            OnBossHealthTotalUpdate?.Invoke(m_sharedHealthTotal);
            if (m_bosses.Count == 1) SetHUDFocusMode(true);
        }

        // 06000b74.
        public bool IsRegistered(Boss boss) => m_bosses.Contains(boss);

        // 06000b75: callback mutation precedes the fresh count and UI reads.
        public void BossDefeated(Boss boss)
        {
            if (!m_bosses.Contains(boss)) return;
            Deregister(boss);
            OnBossDefeated?.Invoke();
            if (m_bosses.Count > 0) return;
            if (m_uiManager.IsOpen(m_HUDContainer)) m_uiManager.GetOrCreate(m_HUDContainer).gameObject.SetActive(false);
            if (m_uiManager.IsOpen(m_onScreenControlsContainer)) m_uiManager.GetOrCreate(m_onScreenControlsContainer).gameObject.SetActive(false);
            SetHUDFocusMode(false);
            if (boss.IsActivePartOfMultiBodyBoss) return;
            ProcessManager.GetSystem<LevelManager>(null, true).TryGetCurrentLevel(out LevelManagerLevel level);
            m_bossesDefeatedByLevelType[level.LevelDefinition.LevelSetupType] = level.LevelDefinition;
            OnBossFullyDefeated?.Invoke();
        }

        // 06000b76: Remove's result is ignored and no health callbacks occur.
        // ARM schedules the last-boss store before health reads; x86 after the
        // paired subtraction. Null/native fault-state equivalence remains held.
        public void Deregister(Boss boss)
        {
            m_bosses.Remove(boss);
            unchecked
            {
                m_sharedHealthTotal -= boss.Definition.Traits.HealthPool;
                m_sharedHealthCurrent -= boss.CurrentHealth;
            }
            m_lastDeregisteredBoss = boss;
            if (m_bosses.Count == 0) SetHUDFocusMode(false);
        }

        // 06000b77: the flag is stored only after the callback, permitting original
        // callback reentry and preserving a callback fault's pre-flag prefix.
        public bool RegisterBossHit()
        {
            if (m_bossHitThisFrame) return false;
            unchecked { --m_sharedHealthCurrent; }
            OnBossHealthPoolChange?.Invoke(m_sharedHealthCurrent);
            m_bossHitThisFrame = true;
            return true;
        }

        // 06000b78 changes current health only.
        public void RestoreHealthToPool(int newTotal)
        {
            m_sharedHealthCurrent = newTotal;
            OnBossHealthPoolChange?.Invoke(newTotal);
        }

        // 06000b79: readiness is set before enumerating the live list.
        private void OnStatesLoaded()
        {
            m_statesLoaded = true;
            foreach (Boss boss in m_bosses) boss.InitialiseBoss();
        }

        // 06000b7a: first match, no Unity-null filtering, foreach disposal.
        public Boss GetBossForAnimator(Animator animator)
        {
            foreach (Boss boss in m_bosses)
                if (boss.Animator.IsAnimator(animator)) return boss;
            return null;
        }

        // 06000b7b: acquire, set position, then Setup with the original collider.
        public Projectile SpawnProjectileFromBoss(ProjectileType definitionProjectile, Boss boss, Vector3 spawnPosition)
        {
            Projectile projectile = m_projectileManager.AcquireProjectile(definitionProjectile);
            projectile.transform.position = spawnPosition;
            projectile.Setup(spawnPosition, boss.ColliderCollision);
            return projectile;
        }

        // 06000b7c: original T has the reference-type constraint. The acquired
        // object is reset before its virtual Initialise(playerDamageable,lifetime).
        public void SpawnObjectFromBoss<T>(PrefabPoolType definitionProjectile, Boss boss, ActorAttachPointType attach, float lifetime) where T : class
        {
            Vector3 position = boss.GetSpawnPoint<T>(attach).position;
            position.y = GetHeightFromRaycast(boss, position);
            Quaternion rotation = Quaternion.LookRotation(boss.ForwardDirection, boss.UpDirection);
            BossTimedObjectPool pool = GetBossPool(definitionProjectile);
            TimedLifetimeObject instance = pool.SpawnInstance(position, rotation, Vector3.one, 1f, null).Instance;
            instance.ResetTimedLifetimeObject();
            instance.Initialise(boss, lifetime);
        }

        // 06000b7d: explicit cast, Add before the destruction subscription;
        // null pools remain inserted and then fault during the callback-field read.
        private BossTimedObjectPool GetBossPool(PrefabPoolType definitionProjectile)
        {
            if (!m_bossWorldSpacePools.TryGetValue(definitionProjectile, out BossTimedObjectPool pool))
            {
                pool = (BossTimedObjectPool)ProcessManager.GetSystem<PrefabPoolManager>(null, true).GetPoolByType(definitionProjectile);
                m_bossWorldSpacePools.Add(definitionProjectile, pool);
                pool.WasDestroyed += RemovePoolOnDestruction;
            }
            return pool;
        }

        // 06000b7e: first Unity-object match, remove delegate before dictionary
        // removal, dispose the enumeration before checking original GUID nullness.
        private void RemovePoolOnDestruction(BossTimedObjectPool pool)
        {
            PrefabPoolType definition = null;
            foreach (KeyValuePair<PrefabPoolType, BossTimedObjectPool> entry in m_bossWorldSpacePools)
            {
                if (entry.Value == pool)
                {
                    definition = entry.Key;
                    pool.WasDestroyed -= RemovePoolOnDestruction;
                    break;
                }
            }
            if (definition != null) m_bossWorldSpacePools.Remove(definition);
        }

        // 06000b7f: actual original ray direction, distance and mask. Exceptional
        // floating-point/native Physics behavior is not proven by source recovery.
        private float GetHeightFromRaycast(Boss boss, Vector3 worldPosition)
        {
            Ray ray = new Ray(worldPosition, -boss.UpDirection);
            if (Physics.Raycast(ray, out RaycastHit hit, worldPosition.y * 2f, boss.GetTraits().TrackMask)) return hit.point.y;
            return worldPosition.y;
        }

        // 06000b80: retained original unguarded fallback to last deregistered boss.
        public Boss GetBossByDefinition(BossDefinition definition)
        {
            foreach (Boss boss in m_bosses)
                if (boss.Definition == definition) return boss;
            return m_lastDeregisteredBoss.Definition == definition ? m_lastDeregisteredBoss : null;
        }

        // 06000b81: predecrement signed 32-bit index only for matching definitions.
        public Boss GetBossByDefinition(BossDefinition definition, int index)
        {
            foreach (Boss boss in m_bosses)
            {
                if (boss.Definition != definition) continue;
                if (unchecked(--index) < 0) return boss;
            }
            return null;
        }

        // 06000b82: actual Unity collider equality OR body-part match; assign the
        // out reference only at a match or after completed nonmatching enumeration.
        public bool IsColliderBoss(Collider otherCollider, out Boss matchedBoss)
        {
            foreach (Boss boss in m_bosses)
            {
                if (boss.ColliderCollision == otherCollider || boss.AnyPartMatchesCollider(otherCollider))
                {
                    matchedBoss = boss;
                    return true;
                }
            }
            matchedBoss = null;
            return false;
        }

        // 06000b83.
        private void Update() { if (m_bossHitThisFrame) m_bossHitThisFrame = false; }

        // 06000b84: original optional null; release states before save unsubscription.
        private void OnShutdown(object context = null)
        {
            if (m_fsmStateLoader != null)
                foreach (FiniteStateMachineScriptableObject stateMachine in m_fsmStateLoader.LoadedStateMachines) stateMachine.ReleaseFSM();
            if (m_saveManagerRef.TryGet(out SaveManager saveManager)) saveManager.RemoveDependantListener(this);
        }

        // 06000b85: real App.Storage generic interface write, no default storage.
        private void SetHUDFocusMode(bool focus) => m_app.Storage.SetValue(AppFSMKeys.UIHUDFocusMode, focus);
        // 06000b86..b88.
        public void SetPhaseContinue() => m_bossContinuePhase = true;
        public bool HasFlagForPhaseContinuation()
        {
            bool result = m_bossContinuePhase;
            if (m_bossContinuePhase) m_bossContinuePhase = false;
            return result;
        }
        public bool BossDefeatedForThisType(LevelSetupTypes setupType) => m_bossesDefeatedByLevelType.ContainsKey(setupType);

        // 06000b89.
        public void BossHardFailure(Boss boss)
        {
            if (m_bosses.Contains(boss)) OnBossHardFailure?.Invoke();
        }

        // 06000b8a ignores saveDataGame and replaces the dictionary before reading
        // unlocked levels. Original Add retains duplicate setup-type faults.
        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            LevelManager levelManager = ProcessManager.GetSystem<LevelManager>(null, true);
            m_bossesDefeatedByLevelType = new Dictionary<LevelSetupTypes, GameplayLevelDefinition>(HardlightEnumComparers.LevelSetupTypesComparer);
            foreach (GameplayLevelDefinition level in levelManager.GetUnlockedLevels())
                if (level.IsBossLevel && level.MissionList.HasAnyCompleteMissions(level))
                    m_bossesDefeatedByLevelType.Add(level.LevelSetupType, level);
        }

        // 06000b8b: both complete native bodies are empty.
        public void OnSaveGameClose(SaveDataGame saveDataGame) { }
        // 06000b8c is supplied by the original ordered field initializers and the
        // genuine MonoBehaviour base constructor; no constructor body is invented.
    }
}
