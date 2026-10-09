using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200025d; full3 constructors +2 overrides, no own fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DisableIfAttribute : InspectorConditionalAttribute
    {
        public DisableIfAttribute(ComparisonType comparisonType, object[] comparisonValues, params string[] fieldNames)
            : base(comparisonType, comparisonValues, fieldNames) { }
        public DisableIfAttribute(ComparisonType comparisonType, object comparisonValue, params string[] fieldNames)
            : base(comparisonType, comparisonValue, fieldNames) { }
        public DisableIfAttribute(string fieldName, object comparisonValue = null)
            : base(fieldName, comparisonValue) { }
        public override bool ShouldShow(bool conditionsMet) => true;
        public override bool ShouldEnable(bool conditionsMet) => !conditionsMet;
    }
}
