using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Multiplier")]
    public class ModifierMultiplier : InputModifier
    {
        [SerializeField] private float m_multiplier = 1f;
        public override float Modify(float value, float deltaTime) { return m_multiplier * value; } // 0600015d: original operand order.
        public ModifierMultiplier() { } // 0600015e
    }
}
