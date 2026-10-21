using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ae: generic App FSM storage comparison.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class RequirementAppFSMValue<T> : RequirementGameplayLevelBase where T : IEquatable<T>
    {
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        [SerializeField] private string m_appFsmKey;
        [SerializeField] private T m_valueToMatch;
        [SerializeField, Tooltip("Value to use if key is not present in App FSM storage")]
        private T m_default;
        private GraphStorageKey m_cachedKey;

        // 0x06001f08 (bool instance 0xbcb4f0): the storage key is resolved from
        // the authored name on first use and recomputed while its ids are all
        // zero. A missing key falls back to the authored default; a missing App
        // faults in SystemRef.Get.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (m_cachedKey.Equals(default(GraphStorageKey)))
                m_cachedKey = new GraphStorageKey(m_appFsmKey, 0, 0);
            if (!m_appRef.Get().Storage.TryGetValue(m_cachedKey, out T value))
                value = m_default;
            return m_valueToMatch.Equals(value);
        }

        // 0x06001f09; the field initializer runs before the ScriptableObject base.
        protected RequirementAppFSMValue() { }
    }
}
