using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [ExecuteInEditMode]
    public class Ribbon : MonoBehaviour
    {
        [SerializeField] private EditableRibbon m_editableRibbon;
        [FormerlySerializedAs("m_optimisedRibbon")]
        [SerializeField] private InertRibbon m_inertRibbon;
        [SerializeField] private SegmentedRibbon m_segmentedRibbon;
        [SerializeField] private RibbonVariantType m_activeRibbonType;

        // Original HLSplines.Runtime 0x06000293. An invalid variant faults even
        // when one of the serialized ribbon references happens to be available.
        public IRibbon GetRibbon()
        {
            switch (m_activeRibbonType)
            {
                case RibbonVariantType.Editable: return m_editableRibbon;
                case RibbonVariantType.Inert: return m_inertRibbon;
                case RibbonVariantType.Segmented: return m_segmentedRibbon;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        // Original 0x06000294. The override is visible during AddComponent and
        // Initialise; the shipping path resets it only after both succeed.
        public IRibbon InitialiseRibbon(int lutCount = 0)
        {
            IRibbon ribbon = GetComponentInChildren<IRibbon>();
            if (ribbon != null) return ribbon;

            GameObject child = new GameObject(transform.name + "_Ribbon");
            child.transform.parent = transform;
            TransformUtils.ResetTransformToIdentity(child.transform);
            EditableRibbon.s_lutCountOverride = lutCount;
            m_editableRibbon = child.AddComponent<EditableRibbon>();
            m_editableRibbon.Initialise();
            EditableRibbon.s_lutCountOverride = 0;
            return m_editableRibbon;
        }

        // Original 0x06000295. Publish the selected type before notifying the
        // old ribbon. Unsupported inputs still reach that original callback.
        public void SetRibbon(IRibbon iRibbon)
        {
            if (iRibbon == null) return;
            IRibbon oldRibbon = GetRibbon();
            if (iRibbon is EditableRibbon editableRibbon)
            {
                m_editableRibbon = editableRibbon;
                m_activeRibbonType = RibbonVariantType.Editable;
            }
            else if (iRibbon is InertRibbon inertRibbon)
            {
                m_inertRibbon = inertRibbon;
                m_activeRibbonType = RibbonVariantType.Inert;
            }
            else if (iRibbon is SegmentedRibbon segmentedRibbon)
            {
                m_segmentedRibbon = segmentedRibbon;
                m_activeRibbonType = RibbonVariantType.Segmented;
            }
            else
            {
                HLOutput.LogError("Attempting to set unsupported ribbon type");
            }
            oldRibbon.UpdateRibbonType(iRibbon);
        }

        // Original 0x06000296 has only the MonoBehaviour base constructor.
        public Ribbon() { }
    }
}
