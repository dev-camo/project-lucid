using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Preserve the original duplicate options and the instantiable registry class.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HardlightEnumComparers
    {
        // Original 060000e0 publishes consent first, then mission. Field initializers retain BeforeFieldInit.
        public static readonly AnalyticsConsentStateEqualityComparer AnalyticsConsentStateComparer = new AnalyticsConsentStateEqualityComparer();
        public static readonly AnalyticsMissionTypeEqualityComparer AnalyticsMissionTypeComparer = new AnalyticsMissionTypeEqualityComparer();
        public HardlightEnumComparers() { }
    }
}
