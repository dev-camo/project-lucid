using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Invert")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ModifierInvert : InputModifier
    {
        // HLInput.Runtime 0600015b: genuine single-precision negation; deltaTime is intentionally unused.
        public override float Modify(float value, float deltaTime) { return -value; }
        // 0600015c: natural public constructor calls the genuine InputModifier/ScriptableObject base chain.
    }
}
