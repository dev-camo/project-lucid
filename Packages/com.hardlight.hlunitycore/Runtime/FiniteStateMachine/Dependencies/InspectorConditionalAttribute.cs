using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [AttributeUsage(AttributeTargets.Field)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class InspectorConditionalAttribute : PropertyAttribute
    {
        public enum ComparisonType { All = 0, Any = 1 }
        public readonly InspectorConditionalField[] ConditionalFields;
        public readonly ComparisonType Comparison;

        // HLUnityCore.Runtime.dll 0x06000f23; ARM64 0x1b20ad4. Original Cartesian
        // product: field names are outermost, values innermost, including nulls.
        protected InspectorConditionalAttribute(ComparisonType comparisonType,
            object[] comparisonValues, params string[] fieldNames)
        {
            ConditionalFields = new InspectorConditionalField[unchecked(fieldNames.Length * comparisonValues.Length)];
            int index = 0;
            for (int fieldIndex = 0; fieldIndex < fieldNames.Length; fieldIndex++)
            {
                if (comparisonValues.Length == 0) continue;
                string fieldName = fieldNames[fieldIndex];
                for (int valueIndex = 0; valueIndex < comparisonValues.Length; valueIndex++)
                    ConditionalFields[index++] = new InspectorConditionalField(fieldName, comparisonValues[valueIndex]);
            }
            Comparison = comparisonType;
        }

        // 0x06000f24; ARM64 0x1b20c40. Each named field retains the same object.
        protected InspectorConditionalAttribute(ComparisonType comparisonType,
            object comparisonValue, params string[] fieldNames)
        {
            ConditionalFields = new InspectorConditionalField[fieldNames.Length];
            for (int i = 0; i < fieldNames.Length; i++)
                ConditionalFields[i] = new InspectorConditionalField(fieldNames[i], comparisonValue);
            Comparison = comparisonType;
        }

        // 0x06000f25; ARM64 0x1b20d58. Always one entry; comparison remains All=0.
        protected InspectorConditionalAttribute(string fieldName, object comparisonValue = null)
        {
            ConditionalFields = new[] { new InspectorConditionalField(fieldName, comparisonValue) };
        }

        // Original abstract contracts 0x06000f26/0x06000f27, no own bodies.
        public abstract bool ShouldShow(bool conditionsMet);
        public abstract bool ShouldEnable(bool conditionsMet);
    }

    public readonly struct InspectorConditionalField
    {
        public readonly string FieldName;
        public readonly object ComparisonValue;
        // 0x06000f28; ARM64 0x1b20e6c. Both arguments retained without validation.
        public InspectorConditionalField(string fieldName, object comparisonValue = null)
        {
            FieldName = fieldName;
            ComparisonValue = comparisonValue;
        }
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ShowIfAttribute : InspectorConditionalAttribute
    {
        // 0x06000f29..0x06000f2b; ARM64 0x1b20ea0/ea4/ea8: exact base tails.
        public ShowIfAttribute(ComparisonType comparisonType, object[] comparisonValues, params string[] fieldNames)
            : base(comparisonType, comparisonValues, fieldNames) { }
        public ShowIfAttribute(ComparisonType comparisonType, object comparisonValue, params string[] fieldNames)
            : base(comparisonType, comparisonValue, fieldNames) { }
        public ShowIfAttribute(string fieldName, object comparisonValue = null)
            : base(fieldName, comparisonValue) { }
        // 0x06000f2c/0x06000f2d; ARM64 0x1b20eac/eb4.
        public override bool ShouldShow(bool conditionsMet) => conditionsMet;
        public override bool ShouldEnable(bool conditionsMet) => true;
    }
}
