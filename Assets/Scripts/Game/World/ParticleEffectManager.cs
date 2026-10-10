using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;

namespace HardlightProject
{
    // Game.Runtime0x02000a8e: full28owner+15natural original declarations.
    // Natural C# closure/iterator emission and exact native body binding are
    // separate unaccepted evidence; no substitute pool/provider is inserted.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ParticleEffectManager : MonoBehaviour, ISystem
    {
        [Tooltip("Where PFX will be spawn and held while they are pooled"), SerializeField]
        private Transform m_pfxHolder;
        [SerializeField] private bool m_actorPFXEnabled;
        private readonly Dictionary<ParticleEffectType, PoolInterest> m_pfxGameObjectPools =
            new Dictionary<ParticleEffectType, PoolInterest>(HardlightProject.HardlightEnumComparers.ParticleEffectTypeComparer);
        private readonly List<ParticleEffectWrapper> m_activePFXGameObjects = new List<ParticleEffectWrapper>();
        private readonly List<ParticleEffectWrapper> m_temporaryPFXList = new List<ParticleEffectWrapper>();
        private readonly List<ParticleEffectWrapper> m_prewarmList = new List<ParticleEffectWrapper>();
        private bool m_preWarming;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private Coroutine m_afterLevelExitCoroutine;
        private readonly YieldInstruction m_yieldAfterLevelExit = new WaitForSeconds(0.1f);

        public bool ActorPFXEnabled => m_actorPFXEnabled; //0x06003cbf

        private void Awake() //0x06003cc0
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        private void OnLevelManagerValid(LevelManager levelManager) //0x06003cc1
        {
            levelManager.OnLevelExit -= OnLevelExit;
            levelManager.OnLevelExit += OnLevelExit;
        }

        private void OnLevelExit() //0x06003cc2
        {
            for (int i = m_activePFXGameObjects.Count - 1; i >= 0; --i)
                m_activePFXGameObjects[i].StopEffectEmittingAndClear();
            this.SafeStopCoroutine(ref m_afterLevelExitCoroutine);
            m_afterLevelExitCoroutine = StartCoroutine(AfterLevelExit());
        }

        public void CleanUpAfterLevel() => OnLevelExit(); //0x06003cc3: same native body after inline.

        private IEnumerator AfterLevelExit() //0x06003cc4 +original iterator0x06003ce4..3ce9.
        {
            yield return new WaitUntil(() => !m_characterManagerRef.IsValid() || !m_characterManagerRef.Get().CharacterValid);
            yield return m_yieldAfterLevelExit;
            foreach (ParticleEffectWrapper pfx in m_activePFXGameObjects)
            {
                if (pfx == null || pfx.gameObject == null) continue;
                UnityEngine.Object.Destroy(pfx.gameObject);
            }
            m_activePFXGameObjects.Clear();
        }

        private void OnDestroy() => ProcessManager.UnregisterSystem(this); //0x06003cc5, no inferred cleanup.

        public void RegisterInterest(ParticleEffectDefinition particleEffectDefinition, UnityEngine.Object interestedObject) //3cc6
        {
            ParticleEffectType pfxType = particleEffectDefinition.PFXType;
            if (!m_pfxGameObjectPools.TryGetValue(pfxType, out PoolInterest poolInterest))
            {
                var pool = new ObjectPool<ParticleEffectWrapper>(
                    () => InstantiatePFX(particleEffectDefinition), OnPoolGet, OnPoolRelease, OnPoolDestroy,
                    false, particleEffectDefinition.InitialPoolSize, 10000);
                poolInterest = new PoolInterest(pool);
                m_pfxGameObjectPools[pfxType] = poolInterest;
            }
            // Includes an already registered pool; original has no try/finally
            // for this flag and permits duplicate interest entries.
            m_preWarming = true;
            while (poolInterest.Pool.CountAll < particleEffectDefinition.InitialPoolSize)
                m_temporaryPFXList.Add(poolInterest.Pool.Get());
            foreach (ParticleEffectWrapper pfx in m_temporaryPFXList)
                poolInterest.Pool.Release(pfx);
            m_temporaryPFXList.Clear();
            m_preWarming = false;
            poolInterest.Interested.Add(interestedObject);
        }

        private void OnPoolGet(ParticleEffectWrapper pfx) { } //0x06003cc7: genuine RET.

        private void OnPoolRelease(ParticleEffectWrapper pfx) //0x06003cc8
        {
            pfx.gameObject.SetActive(false);
            pfx.transform.SetParent(m_pfxHolder);
            // Both architectures pass seven zeros, including quaternion.w.
            pfx.transform.SetPositionAndRotation(Vector3.zero, default(Quaternion));
        }

        private void OnPoolDestroy(ParticleEffectWrapper pfx) => UnityEngine.Object.Destroy(pfx); //3cc9

        public void UnregisterInterest(ParticleEffectDefinition particleEffectDefinition, UnityEngine.Object interestedObject) //3cca
        {
            if (m_pfxGameObjectPools.TryGetValue(particleEffectDefinition.PFXType, out PoolInterest poolInterest))
                poolInterest.Interested.Remove(interestedObject);
        }

        public void CleanPools() //0x06003ccb: destroy before deferred dictionary removals.
        {
            var poolsToRemove = new List<ParticleEffectType>();
            foreach (var (pfxType, poolInterest) in m_pfxGameObjectPools)
            {
                if (poolInterest.Interested.Count > 0) continue;
                poolInterest.Pool.Dispose();
                poolsToRemove.Add(pfxType);
            }
            foreach (ParticleEffectType pfxType in poolsToRemove)
                m_pfxGameObjectPools.Remove(pfxType);
        }

        private ParticleEffectWrapper InstantiatePFX(ParticleEffectDefinition definition) //0x06003ccc
        {
            ParticleEffectWrapper pfx = UnityEngine.Object.Instantiate(definition.PFX, m_pfxHolder);
            pfx.gameObject.SetActive(false);
            return pfx;
        }

        private bool GetFromPool(ParticleEffectType pfxType, out ParticleEffectWrapper pfx) //0x06003ccd
        {
            bool found = m_pfxGameObjectPools.TryGetValue(pfxType, out PoolInterest poolInterest);
            pfx = found ? poolInterest.Pool.Get() : null;
            return found;
        }

        private void ReturnToPool(ParticleEffectWrapper pfx) =>
            m_pfxGameObjectPools[pfx.ParticleEffectDefinition.PFXType].Pool.Release(pfx); //0x06003cce

        public bool AcquirePFX(ParticleEffectDefinition particleEffectDefinition, Action<ParticleEffectWrapper> onComplete,
            ParticleEffectWrapper.ParticleBindingParameters particleBindingParameters, out ParticleEffectWrapper pfx) //3ccf
        {
            bool acquired = GetFromPool(particleEffectDefinition.PFXType, out pfx);
            if (acquired)
            {
                pfx.SetUp(Callback, particleEffectDefinition);
                pfx.UpdateParameters(particleBindingParameters);
                m_activePFXGameObjects.Add(pfx);
            }
            else onComplete?.Invoke(null);
            return acquired;

            void Callback(ParticleEffectWrapper pfx) //genuine0x06003cdf callback+original closure ctor3cde.
            {
                m_activePFXGameObjects.Remove(pfx);
                ReturnToPool(pfx);
                onComplete?.Invoke(pfx);
            }
        }

        public void AcquirePFX(ActorParticleEffectDefinition particleEffectDefinition,
            Action<ParticleEffectWrapper, ActorParticleEffectDefinition, bool> onStart,
            Action<ParticleEffectWrapper> onComplete,
            ParticleEffectWrapper.ParticleBindingParameters particleBindingParameters, bool startEffect) //0x06003cd0
        {
            ParticleEffectType pfxType = particleEffectDefinition.PFXType;
            ParticleEffectWrapper pfx;
            if (particleEffectDefinition.CanInterrupt && m_activePFXGameObjects.TryFind(
                activePfx => activePfx.ParticleEffectDefinition.PFXType == pfxType, out pfx))
            {
                StopPFX(pfx, true);
                return;
            }
            OnStartCallback();

            void OnStartCallback() //0x06003ce2
            {
                if (GetFromPool(pfxType, out pfx))
                {
                    pfx.SetUp(OnCompleteCallback, particleEffectDefinition);
                    pfx.UpdateParameters(particleBindingParameters);
                    m_activePFXGameObjects.Add(pfx);
                    onStart?.Invoke(pfx, particleEffectDefinition, startEffect);
                }
                else onComplete?.Invoke(null);
            }

            void OnCompleteCallback(ParticleEffectWrapper pfx) //0x06003ce3
            {
                m_activePFXGameObjects.Remove(pfx);
                ReturnToPool(pfx);
                onComplete?.Invoke(pfx);
                // Reread only after pool release and the user callback.
                if (pfx.AcquireOnComplete) OnStartCallback();
            }
        }

        public void StopPFX(ParticleEffectWrapper particleEffect, bool acquireOnComplete = false) //0x06003cd1
        {
            foreach (ParticleEffectWrapper pfx in m_activePFXGameObjects)
            {
                if (pfx.ParticleEffectDefinition.PFXType != particleEffect.ParticleEffectDefinition.PFXType ||
                    pfx.Status != ParticleEffectWrapper.ParticleEffectWrapperStatus.Started || pfx != particleEffect) continue;
                pfx.StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear, acquireOnComplete);
                break;
            }
        }

        public void StartPrewarmParticleSystems(Transform target) //0x06003cd2
        {
            foreach (var entry in m_pfxGameObjectPools)
            {
                ParticleEffectDefinition definition = ProcessManager.GetSystem<DataManager>(null, true)
                    .ParticleEffectDefinitions[entry.Key];
                if (!AcquirePFX(definition, null, new ParticleEffectWrapper.ParticleBindingParameters
                    { ActorVisibility = true }, out ParticleEffectWrapper pfx)) continue;
                pfx.SetPositionAndRotation(target, new Vector3(0f, -10f, 0f), true);
                pfx.StartEffect();
                m_prewarmList.Add(pfx);
            }
        }

        public void StopPrewarmParticleSystems() //0x06003cd3
        {
            foreach (ParticleEffectWrapper pfx in m_prewarmList) pfx.StopEffectEmittingAndClear();
            m_prewarmList.Clear();
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void SetupDebugMenu() //0x06003cd4: shipped body still performs enum enumeration only.
        {
            EnumUtilities.GetValues<ParticleEffectType>();
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugSpawnEffect(ParticleEffectType particleType) //0x06003cd5
        {
            ParticleEffectWrapper pfx = null;
            Character character = null;
            CharacterManager characterManager = ProcessManager.GetSystemSafe<CharacterManager>(null, true);
            if (ReferenceEquals(characterManager, null) || !characterManager.TryGetCurrentCharacter(out character)) return;
            Vector3 localVelocity = character.IsVelocityPaused
                ? character.WorldToLocalRotation * character.GetPausedWorldVelocity() : character.LocalVelocity;
            float xzVelocity = localVelocity.xz().magnitude;
            ParticleEffectDefinition definition = ProcessManager.GetSystem<DataManager>(null, true).ParticleEffectDefinitions[particleType];
            float brainMovement = character.BrainMovementMagnitude;
            if (!m_pfxGameObjectPools.ContainsKey(particleType)) RegisterInterest(definition, this);
            var parameters = new ParticleEffectWrapper.ParticleBindingParameters
            {
                ActorImpactVelocity = xzVelocity,
                ActorXZVelocity = xzVelocity,
                ActorVisibility = true,
                BrainMovement = brainMovement,
                SpinDashCharge = 1f
            };
            if (!AcquirePFX(definition, null, parameters, out pfx)) return;
            pfx.transform.SetPositionAndRotation(character.WorldPosition, character.WorldRotation);
            pfx.StartEffect();
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugMenu() { } //0x06003cd6: genuine RET.
        private string GetDebugMenuActorPFXToggleText() =>
            string.Concat("Toggle actor PFX [", m_actorPFXEnabled ? "ON" : "OFF", "]"); //0x06003cd7
        private string GetDebugMenuStopButtonText() =>
            string.Format("Stop active PFX ({0})", m_activePFXGameObjects.Count); //0x06003cd8
        public ParticleEffectManager() { } //0x06003cd9: exact initialized field order before original base.

        private struct PoolInterest //0x02000a8f, true value type, no substitute class.
        {
            public readonly ObjectPool<ParticleEffectWrapper> Pool;
            public readonly List<UnityEngine.Object> Interested;
            public PoolInterest(ObjectPool<ParticleEffectWrapper> pool) //0x06003cdb: Pool first, list second.
            {
                Pool = pool;
                Interested = new List<UnityEngine.Object>();
            }
        }
    }
}
