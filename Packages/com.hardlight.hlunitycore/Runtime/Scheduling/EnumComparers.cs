using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    public enum LeaderboardType : long { Classic = 0L, Recurring = 1L }

    public class LeaderboardTypeEqualityComparer : IEqualityComparer<LeaderboardType>
    {
        // Original 060005c8 / ARM64 1ad9a28 compares all 64 bits.
        public bool Equals(LeaderboardType a, LeaderboardType b) { return a == b; }
        // 060005c9 / 1ad9a34 boxes and calls Convert.ToInt32(Object). Large enum
        // values throw rather than truncate or use Int64.GetHashCode.
        public int GetHashCode(LeaderboardType a) { return Convert.ToInt32(a); }
        public LeaderboardTypeEqualityComparer() { }
    }

    // Original 0200010a is a nonstatic BeforeFieldInit class with an instance
    // constructor. Field initializers generate its original ordered .cctor.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnumComparers
    {
        public static readonly LeaderboardTypeEqualityComparer LeaderboardTypeComparer = new LeaderboardTypeEqualityComparer();
        public static readonly UpdateOnEqualityComparer UpdateOnComparer = new UpdateOnEqualityComparer();
        public EnumComparers() { }
        // Generated original 060005cc / ARM64 1ad9adc allocates leader then update.
    }

    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UpdateOnEqualityComparer : IEqualityComparer<UpdateOn>
    {
        // Original 06000ee7..ee9 / ARM64 1b1ff04..18.
        public bool Equals(UpdateOn a, UpdateOn b) { return a == b; }
        public int GetHashCode(UpdateOn a) { return (int)a; }
        public UpdateOnEqualityComparer() { }
    }
}
