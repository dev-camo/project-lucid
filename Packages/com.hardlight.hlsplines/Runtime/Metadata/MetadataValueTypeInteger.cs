using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [CreateAssetMenu(fileName = "MetadataValueTypeInteger", menuName = "Hardlight/Metadata/MetadataValueTypeInteger", order = 0)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MetadataValueTypeInteger : MetadataValueType
    {
        [SerializeField] private bool m_hasMinimum;
        [SerializeField] private int m_minimum;
        [SerializeField] private bool m_hasMaximum;
        [SerializeField] private int m_maximum;
        [SerializeField] private bool m_noDefaultHelpText;
        [SerializeField] private int m_default;

        protected override void Reset()
        {
            base.Reset();
            m_label = "Integer";
        }
        public override string GetDefaultAsString() => m_default.ToString();
        public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
        {
            // Read and parse a then b. Inspector bounds do not constrain this authored interpolation.
            int value = MetadataUtilities.InterpolateValue(a.AsInt(), b.AsInt(), t, type);
            // The key is evaluated before ToString; only publish out after construction completes.
            newValue = new Metadata(a.Key, value.ToString());
        }
        public MetadataValueTypeInteger() { }
    }
}
