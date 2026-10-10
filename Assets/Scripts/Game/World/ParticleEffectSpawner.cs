using System;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Game.Runtime 0x02000a94; all twelve original declarations0x06003cea..3cf5.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ParticleEffectSpawner : MonoBehaviour
    {
        [SerializeField, HashEnum(typeof(ParticleEffectType))] private ParticleEffectType m_particleEffectType;
        [SerializeField] private bool m_spawnOnCharacter;
        [SerializeField] private Transform m_spawnPoint;
        [SerializeField] private Vector3 m_spawnOffset;
        [Tooltip("Particle system will continuously follow spawn point position and rotation for its lifetime."), SerializeField]
        private bool m_followSpawnPointTransform;
        [SerializeField] private bool m_stopParticleOnDisable;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private readonly SystemRef<ParticleEffectManager> m_particleEffectManagerRef =
            ProcessManager.GetSystemRef<ParticleEffectManager>(null, true);
        private ParticleEffectDefinition m_effectDefinition;
        private ParticleEffectWrapper m_currentEffect;
        private Action m_onCompleteCallback;
        private bool m_initialised;

        private void Awake() // 0x06003cea
        {
            if (m_spawnOnCharacter)
                m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        }

        private void OnEnable() => m_particleEffectManagerRef.InvokeOnValid(ParticleEffectManagerValid); //3ceb

        private void OnDisable() //0x06003cec; a missing manager leaves initialised unchanged.
        {
            if (m_particleEffectManagerRef.IsNull()) return;
            if (m_stopParticleOnDisable) StopAndClear();
            m_particleEffectManagerRef.Get().UnregisterInterest(m_effectDefinition, this);
            m_initialised = false;
        }

        private void Reset() //0x06003ced; real Unity-null check.
        {
            if (m_spawnPoint == null) m_spawnPoint = transform;
        }

        private void ParticleEffectManagerValid(ParticleEffectManager particleEffectManager) =>
            Initialise(particleEffectManager); //0x06003cee

        private void Initialise(ParticleEffectManager particleEffectManager) //0x06003cef
        {
            if (ProcessManager.GetSystem<App>(null, true).DataManager.ParticleEffectDefinitions
                .TryGetValue(m_particleEffectType, out m_effectDefinition))
            {
                particleEffectManager.RegisterInterest(m_effectDefinition, this);
                m_initialised = true;
            }
        }

        public void Spawn() //0x06003cf0
        {
            Character character = null;
            if (m_spawnPoint == null || m_particleEffectManagerRef.IsNull())
            {
                m_onCompleteCallback?.Invoke();
                return;
            }
            ParticleEffectManager particleEffectManager = m_particleEffectManagerRef.Get();
            if (!m_initialised) Initialise(particleEffectManager);
            if (m_currentEffect != null) StopAndClear();
            // Original ignores this boolean. Null or stale output faults later;
            // no provider fallback or callback completion is added here.
            particleEffectManager.AcquirePFX(m_effectDefinition, OnComplete,
                default(ParticleEffectWrapper.ParticleBindingParameters), out m_currentEffect);
            if (m_spawnOnCharacter && m_characterManagerRef.Get().TryGetCurrentCharacter(out character))
                SetSpawnPoint(character.transform, m_spawnOffset);
            m_currentEffect.SetPositionAndRotation(m_spawnPoint, m_spawnOffset, m_followSpawnPointTransform);
            m_currentEffect.StartEffect();
        }

        public void Spawn(Action onComplete) //0x06003cf1
        {
            if (m_particleEffectManagerRef.IsNull())
            {
                onComplete?.Invoke();
                return;
            }
            m_onCompleteCallback = onComplete;
            Spawn();
        }

        public void StopAndClear() //0x06003cf2; callback can reenter before the final null store.
        {
            if (m_currentEffect == null) return;
            m_currentEffect.StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear, false);
            m_currentEffect = null;
        }

        public void SetSpawnPoint(Transform target, Vector3 offset) //0x06003cf3
        {
            m_spawnPoint = target;
            m_spawnOffset = offset;
        }

        private void OnComplete(ParticleEffectWrapper effectWrapper) //0x06003cf4
        {
            if (m_currentEffect == effectWrapper) m_currentEffect = null;
            m_onCompleteCallback?.Invoke();
        }

        public ParticleEffectSpawner() { } //0x06003cf5: original SystemRef before base; no true defaults.
    }
}
