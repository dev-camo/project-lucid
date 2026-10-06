using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HideIfAttribute : InspectorConditionalAttribute
    {
        public HideIfAttribute(ComparisonType comparisonType, object[] comparisonValues,
            params string[] fieldNames) : base(comparisonType, comparisonValues, fieldNames) { }
        public HideIfAttribute(ComparisonType comparisonType, object comparisonValue,
            params string[] fieldNames) : base(comparisonType, comparisonValue, fieldNames) { }
        public HideIfAttribute(string fieldName, object comparisonValue = null)
            : base(fieldName, comparisonValue) { }
        public override bool ShouldShow(bool conditionsMet) => !conditionsMet;
        public override bool ShouldEnable(bool conditionsMet) => true;
    }
}
