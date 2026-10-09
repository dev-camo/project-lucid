using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Set Delta")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierSetDelta : InputModifier
    {
        [SerializeField] private float m_newDelta;
        public override float Modify(float value, float deltaTime) // 06000168
        {
            if ((value > 0f && m_newDelta > 0f) || (value < 0f && m_newDelta < 0f)) return m_newDelta;
            return value;
        }
        public ModifierSetDelta() { } // 06000169
    }
}
