using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000622; native 0x569EA0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SequencedMaterialProperty : SequencedMaterial
    {
        [SerializeField] private string m_propertyName;
        private int m_propertyHash;

        // Original060020dc: the authored float property's current value becomes the reset value.
        private void Awake()
        {
            m_propertyHash = Shader.PropertyToID(m_propertyName);
            m_blendValueReset = m_material.GetFloat(m_propertyHash);
        }

        // Original060020dd.
        protected override void SetBlendValue(float value)
        {
            m_material.SetFloat(m_propertyHash, value);
        }

        // Implicit original060020de.
    }
}
