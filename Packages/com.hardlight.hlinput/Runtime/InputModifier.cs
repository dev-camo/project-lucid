using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InputModifier : ScriptableObject
    {
        public InputModifierType ModifierType;
        public abstract float Modify(float value, float deltaTime); // 0600013c: genuine abstract contract0.
        public virtual float Modify(float value, float deltaTime, GameInput gameInput) { return Modify(value, deltaTime); } // 0600013d
        // 0600013e/13f are genuine original identity/empty bodies, not recovery placeholders.
        public virtual Vector3 Modify(Vector3 value, float deltaTime) { return value; }
        public virtual void Reset() { }
        protected InputModifier() { } // 06000140: original base-only ctor.
        public enum InputModifierType { Float = 0, Vector3 = 1 }
    }
}
