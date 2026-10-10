using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ParticleEffectDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ParticleEffectDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ParticleEffectDefinition : ScriptableObject
    {
        [HashEnum(typeof(ParticleEffectType)), SerializeField] private ParticleEffectType m_pfxType;
        [SerializeField] private GameObject m_pfx;
        [Tooltip("Initial allocated size of the spawn pool."), SerializeField, Min(1)]
        private int m_initialPoolSize = 1;
        private ParticleEffectWrapper m_pfxWrapper;

        // Game.Runtime 06001e8d..8f: read the original cached fields.
        public ParticleEffectType PFXType => m_pfxType;
        public ParticleEffectWrapper PFX => m_pfxWrapper;
        public int InitialPoolSize => m_initialPoolSize;
        private void Awake() { UpdateCachedValues(); } // 06001e90, virtual dispatch.
        private void OnValidate() // 06001e91: original player skips validation in batch mode.
        {
            if (Application.isBatchMode) return;
            UpdateCachedValues();
        }
        protected virtual void UpdateCachedValues() // 06001e92.
        {
            if (m_pfx == null) m_pfxWrapper = null;
            else
            {
                m_pfxWrapper = m_pfx.GetComponent<ParticleEffectWrapper>();
                // The supplied release retains this otherwise unused Unity comparison.
                _ = m_pfxWrapper == null;
            }
        }
        public ParticleEffectDefinition() { } // 06001e93; pool size initializes before base.
    }
}
