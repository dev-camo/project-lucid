using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime owner 0x02000072. Inferred from both complete native bodies.
    // Its genuine concrete spline providers remain a separate, unresolved source frontier.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Spline : MonoBehaviour
    {
        [SerializeField] private EditableSpline m_editableSpline;
        [SerializeField, FormerlySerializedAs("m_optimisedSpline")] private InertSpline m_inertSpline;
        [SerializeField] private SegmentedSpline m_segmentedSpline;
        [SerializeField] private SplineVariantType m_activeSplineType;

        // 0x060002f5: selection is solely the serialized discriminator; invalid values throw.
        public ISpline GetSpline()
        {
            switch (m_activeSplineType)
            {
                case SplineVariantType.Editable: return m_editableSpline;
                case SplineVariantType.Inert: return m_inertSpline;
                case SplineVariantType.Segmented: return m_segmentedSpline;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        // 0x060002f6: existing interface uses a CLR null check. Creating the child does not
        // change m_activeSplineType or call SetSpline; the newly stored editable field is returned.
        public ISpline InitialiseSpline()
        {
            ISpline existing = GetComponentInChildren<ISpline>();
            if (existing != null) return existing;
            GameObject child = new GameObject(transform.name + "_Spline");
            child.transform.parent = transform;
            TransformUtils.ResetTransformToIdentity(child.transform);
            m_editableSpline = child.AddComponent<EditableSpline>();
            return m_editableSpline;
        }

        // 0x060002f7: capture the old selected spline before replacing any field. The old
        // reference receives UpdateSplineType even on unsupported input after its error log.
        // Null old selection therefore faults after a supported replacement has been stored.
        public void SetSpline(ISpline iSpline)
        {
            if (iSpline == null) return;
            ISpline oldSpline = GetSpline();
            if (iSpline is EditableSpline editable)
            {
                m_editableSpline = editable;
                m_activeSplineType = SplineVariantType.Editable;
            }
            else if (iSpline is InertSpline inert)
            {
                m_inertSpline = inert;
                m_activeSplineType = SplineVariantType.Inert;
            }
            else if (iSpline is SegmentedSpline segmented)
            {
                m_segmentedSpline = segmented;
                m_activeSplineType = SplineVariantType.Segmented;
            }
            else HLOutput.LogError("Attempting to set unsupported spline type", null);
            oldSpline.UpdateSplineType(iSpline);
        }

        // 0x060002f8: the original constructor only calls MonoBehaviour's constructor.
        public Spline() { }
    }
}
