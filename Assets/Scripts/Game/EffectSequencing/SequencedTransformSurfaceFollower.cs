using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200062a; native 0x56ACB0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedTransformSurfaceFollower : SequencedEffect
    {
        [SerializeField] private TransformSurfaceFollower m_transformSurfaceFollower;

        // Original060020fc: listen for the follower's completion, then start it.
        protected override void OnEffectTriggered(IEffectData data)
        {
            m_transformSurfaceFollower.OnCompleteAction += OnComplete;
            m_transformSurfaceFollower.SetEnabled(true);
        }

        // Original060020fd: only the sequenced follower's own completion unsubscribes and completes the effect.
        private void OnComplete(TransformSurfaceFollower transformSurfaceFollower)
        {
            if (m_transformSurfaceFollower == transformSurfaceFollower)
            {
                m_transformSurfaceFollower.OnCompleteAction -= OnComplete;
                m_onComplete?.Invoke();
            }
        }

        // Implicit original060020fe.
    }
}
