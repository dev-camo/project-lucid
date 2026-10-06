using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CollectableTier
    {
        public int Index { get; set; }
        public int MaxDamage { get; set; }
        public int StartingCount { get; set; }
        public float ReviveMultiplier { get; set; }

        // Original 06003b34 leaves all fields at their CLR defaults. The native
        // accessors perform direct loads/stores without validating tier values.
        public CollectableTier() { }
    }
}
