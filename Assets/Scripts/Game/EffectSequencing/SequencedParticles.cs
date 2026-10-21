using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000624 (+ iterator 02000625); native 0x56A130.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedParticles : SequencedEffect
    {
        [SerializeField] private bool m_useParticleEffectWrapper;
        [SerializeField]
        [HideIf("m_useParticleEffectWrapper", (string)null)]
        private ParticleSystem m_particleSystem;
        [SerializeField]
        [HideIf("m_useParticleEffectWrapper", (string)null)]
        private bool m_playOnTarget;
        [SerializeField]
        [ShowIf("m_useParticleEffectWrapper", (string)null)]
        private ParticleEffectWrapper m_particleEffectWrapper;
        [SerializeField] private bool m_waitForParticleCompletion = true;

        // Original060020e2: a wrapper reports completion through its own callback; a plain system is watched by a
        // coroutine, which is skipped (and completes at once) when the host is inactive.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (m_useParticleEffectWrapper)
            {
                if (m_waitForParticleCompletion) m_particleEffectWrapper.SetUp(_ => m_onComplete(), null);
                m_particleEffectWrapper.StartEffect();
                if (m_waitForParticleCompletion) return;
            }
            else
            {
                m_particleSystem.Play(true);
                TargetedEffect targetedEffect = (TargetedEffect)data;
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(WaitForParticles(targetedEffect));
                    if (m_waitForParticleCompletion) return;
                }
            }
            m_onComplete();
        }

        // Original060020e3 and iterator 02000625 (MoveNext 0x56A410): optionally keep the system on the target
        // while it plays, then stop it and complete when waiting was requested.
        private IEnumerator WaitForParticles(TargetedEffect data)
        {
            while (m_particleSystem.isPlaying)
            {
                if (m_playOnTarget)
                {
                    Vector3 position = data.Target.position + data.Target.up * data.Offset.y;
                    m_particleSystem.transform.SetPositionAndRotation(position, data.Target.rotation);
                }
                yield return null;
            }
            m_particleSystem.Stop();
            if (m_waitForParticleCompletion) m_onComplete();
        }

        // Original060020e4: the reference that the current mode does not use is cleared.
        private void OnValidate()
        {
            if (m_useParticleEffectWrapper) m_particleSystem = null;
            else m_particleEffectWrapper = null;
        }

        // Implicit original060020e5: m_waitForParticleCompletion starts true.
    }
}
