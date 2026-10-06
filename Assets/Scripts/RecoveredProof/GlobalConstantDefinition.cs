using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class GlobalConstantDefinition : ScriptableObject
    {
        [SerializeField] private GlobalConstantType m_globalConstantType;
        public GlobalConstantType Type { get { return m_globalConstantType; } } // 06001d0a
        protected virtual void Awake() { UpdateCachedValues(); } // 06001d0b: virtual dispatch.
        protected virtual void OnValidate() { UpdateCachedValues(); } // 06001d0c: same dispatch.
        // 06001d0d is a genuine empty original virtual body, not a recovery placeholder.
        protected virtual void UpdateCachedValues() { }
        protected GlobalConstantDefinition() { } // 06001d0e: original base-only ctor.
    }
}
