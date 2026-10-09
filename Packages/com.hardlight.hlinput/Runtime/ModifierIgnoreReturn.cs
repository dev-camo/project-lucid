using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Ignore Return")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierIgnoreReturn : InputModifier
    {
        private float m_valuePrevious;
        public override float Modify(float value, float deltaTime) // 06000158
        {
            if ((value > 0f && m_valuePrevious > value) || (value < 0f && m_valuePrevious < value)) return m_valuePrevious;
            m_valuePrevious = value;
            return value;
        }
        public override void Reset() { m_valuePrevious = 0f; } // 06000159
        public ModifierIgnoreReturn() { } // 0600015a
    }
}
