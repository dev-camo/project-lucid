using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CascadeGroup
    {
        public float ExpiryTimer;
        public readonly float LifetimeSeconds;
        public readonly List<PooledPrefab<PooledCascadeObject>> CascadeObjects;

        // Original 06003de7: assign lifetime, reset elapsed time, then allocate list.
        public CascadeGroup(float lifetimeSeconds)
        {
            LifetimeSeconds = lifetimeSeconds;
            ExpiryTimer = 0f;
            CascadeObjects = new List<PooledPrefab<PooledCascadeObject>>();
        }

        // Original 06003de8 accumulates the supplied delta without clamping.
        public void UpdateTimer(float deltaTime) { ExpiryTimer += deltaTime; }
    }
}
