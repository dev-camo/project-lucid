using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000623; native 0x569F10.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedParticleEffectSpawner : SequencedEffect
    {
        [SerializeField] private ParticleEffectSpawner m_effectSpawner;
        [Tooltip("If effect data is a targeted effect then use target and offset for spawned particle system.")]
        [SerializeField]
        private bool m_overrideSpawnWithTargetedEffect;
        [SerializeField]
        [Tooltip("Will wait for particle system to stop before calling complete callback.")]
        private bool m_waitForParticleEffectToFinish;
        [SerializeField]
        [Tooltip("Multiplied by targeted effect offset.")]
        private Vector3 m_offsetMultiplier = Vector3.one;

        // Original060020df: waiting hands the completion callback to the spawner; otherwise the effect completes at once.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (m_overrideSpawnWithTargetedEffect && data is TargetedEffect targetedEffect && targetedEffect.Target != null)
                m_effectSpawner.SetSpawnPoint(targetedEffect.Target, Vector3.Scale(m_offsetMultiplier, targetedEffect.Offset));
            if (m_waitForParticleEffectToFinish)
            {
                m_effectSpawner.Spawn(m_onComplete);
                return;
            }
            m_effectSpawner.Spawn();
            m_onComplete?.Invoke();
        }

        // Original060020e0: editor reset finds the sibling spawner.
        private void Reset()
        {
            if (m_effectSpawner == null) m_effectSpawner = GetComponent<ParticleEffectSpawner>();
        }

        // Implicit original060020e1.
    }
}
