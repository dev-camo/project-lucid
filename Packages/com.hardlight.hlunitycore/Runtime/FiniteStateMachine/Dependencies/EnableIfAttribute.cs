using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200025e; full3 constructors +2 overrides, no own fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnableIfAttribute : InspectorConditionalAttribute
    {
        public EnableIfAttribute(ComparisonType comparisonType, object[] comparisonValues, params string[] fieldNames)
            : base(comparisonType, comparisonValues, fieldNames) { }
        public EnableIfAttribute(ComparisonType comparisonType, object comparisonValue, params string[] fieldNames)
            : base(comparisonType, comparisonValue, fieldNames) { }
        public EnableIfAttribute(string fieldName, object comparisonValue = null)
            : base(fieldName, comparisonValue) { }
        public override bool ShouldShow(bool conditionsMet) => true;
        public override bool ShouldEnable(bool conditionsMet) => conditionsMet;
    }
}
