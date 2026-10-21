using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000621; native 0x569E20.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedMaterialAlpha : SequencedMaterial
    {
        // Original060020d9: the material's current alpha becomes the reset value.
        private void Awake()
        {
            m_blendValueReset = m_material.color.a;
        }

        // Original060020da: only the alpha channel of the material colour changes.
        protected override void SetBlendValue(float value)
        {
            Color color = m_material.color;
            color.a = value;
            m_material.color = color;
        }

        // Original060020db: base constructor only.
    }
}
