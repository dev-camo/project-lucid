using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006de: an ornament posed by an Animator.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrnamentAdvanced : Ornament
    {
        [SerializeField]
        private AnimatorSampler m_animatorSampler;

        // 0600250b
        public AnimatorSampler AnimatorSampler => m_animatorSampler;

        // 0600250c (0x59D200): the base fills its references first; the sampler is then taken from the object or added.
        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_animatorSampler == null && !gameObject.TryGetComponent(out m_animatorSampler))
                m_animatorSampler = gameObject.AddComponent<AnimatorSampler>();
        }
    }
}
