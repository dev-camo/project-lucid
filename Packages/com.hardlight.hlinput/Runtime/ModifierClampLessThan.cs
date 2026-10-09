using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Clamp Less Than")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierClampLessThan : InputModifier
    {
        [SerializeField] private float m_maximum;
        public override float Modify(float value, float deltaTime) { return value < m_maximum ? value : m_maximum; } // 06000146: ordered, NaN selects maximum.
        public ModifierClampLessThan() { } // 06000147
    }
}
