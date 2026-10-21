using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006e3: an ornament posed by an Animation clip.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrnamentSimple : Ornament
    {
        [SerializeField]
        private AnimationSampler m_animationSampler;

        // 06002535
        public AnimationSampler AnimationSampler => m_animationSampler;

        // 06002536 (0x5A0090): as OrnamentAdvanced.OnValidate, for the animation sampler.
        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_animationSampler == null && !gameObject.TryGetComponent(out m_animationSampler))
                m_animationSampler = gameObject.AddComponent<AnimationSampler>();
        }

        // 06002537: explicit so that the constructor keeps its original place after the other members.
        public OrnamentSimple() { }
    }
}
