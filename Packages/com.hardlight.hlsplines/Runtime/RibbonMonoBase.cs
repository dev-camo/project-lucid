using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class RibbonMonoBase : MonoBehaviour
    {
        [SerializeField] protected SplineType m_centerSplineType = SplineType.CatmullRom;
        [SerializeField] protected SplineType m_sideSplineType = SplineType.CatmullRom;
        [SerializeField] protected internal MetadataGroups m_metadata;
        [SerializeField] protected Bounds m_boundingBox;
        protected List<Bounds> m_knotBounds;
        protected List<SortKnots> m_sortedKnots;
        protected readonly Vector3[] m_boundsCorners = new Vector3[8];

        // Original HLSplines.Runtime 0x060004b2: return the original stored metadata.
        public MetadataGroups Metadata => m_metadata;

        // Original 0x060004b3. Counts use knotCount-1 without clamping; the arrays
        // populate actual list elements rather than setting list capacity alone.
        protected void InitKnotSortStorage(IRibbonRuntimeHandle handle, int knotCount, Vector3 localPosition)
        {
            if (m_knotBounds == null || m_knotBounds.Count != knotCount - 1)
            {
                m_knotBounds = new List<Bounds>(new Bounds[knotCount - 1]);
                RibbonRuntimeComponent.FillKnotBounds(handle, m_knotBounds);
            }
            if (m_sortedKnots == null || m_sortedKnots.Count != knotCount - 1)
                m_sortedKnots = new List<SortKnots>(new SortKnots[knotCount - 1]);
            RibbonRuntimeComponent.SortKnotBounds(handle, localPosition, m_knotBounds, m_sortedKnots);
        }

        // Original 0x060004b4 clears the bounds list before the sorting list.
        protected void ClearKnotSortStorage()
        {
            m_knotBounds = null;
            m_sortedKnots = null;
        }
    }
}
