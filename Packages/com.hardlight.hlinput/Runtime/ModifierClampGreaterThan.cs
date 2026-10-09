using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Clamp Greater Than")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierClampGreaterThan : InputModifier
    {
        [SerializeField] private float m_minimum;
        public override float Modify(float value, float deltaTime) { return value > m_minimum ? value : m_minimum; } // 06000144: ordered, NaN selects minimum.
        public ModifierClampGreaterThan() { } // 06000145
    }
}
