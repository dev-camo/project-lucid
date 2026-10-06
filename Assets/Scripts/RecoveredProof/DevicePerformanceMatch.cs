using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DevicePerformanceMatch : ISerializationCallbackReceiver
    {
        [HideInInspector]
        [SerializeField] protected string name;
        [SerializeField] private PerformanceProfile m_profile;

        // Original06002d95: Object base then the same profile reference.
        public DevicePerformanceMatch(PerformanceProfile profile) { m_profile = profile; }
        public PerformanceProfile Profile { get { return m_profile; } }
        // Original06002d97/98 are genuine RET bodies.
        public virtual void OnBeforeSerialize() { }
        public virtual void OnAfterDeserialize() { }
    }
}
