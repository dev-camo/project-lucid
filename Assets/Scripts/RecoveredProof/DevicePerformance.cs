using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DevicePerformance : ScriptableObject
    {
        private RuntimePlatform m_platform;
        [SerializeField] protected PerformanceProfile m_fallback;

        // Game.Runtime 0x06002d92; the original platform field has no SerializeField.
        public virtual RuntimePlatform Platform => m_platform;
        // 0x06002d93: base class returns the configured fallback without hardware queries.
        public virtual PerformanceProfile GetMatch() { return m_fallback; }
        // 0x06002d94: native ScriptableObject-base-only constructor.
    }
}
