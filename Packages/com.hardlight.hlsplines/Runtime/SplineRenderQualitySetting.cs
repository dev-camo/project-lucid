using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public class SplineRenderQualitySetting
    {
        [SerializeField, Header("Surface Settings")] protected bool m_isSelected;
        [SerializeField] protected int m_splineDrawDistance = 1000;
        [SerializeField] protected bool m_displayKnotHandles;
        [SerializeField] protected Color m_knotHeadColour = Color.white;
        [SerializeField] protected Color m_knotTailColour = Color.grey;
        [SerializeField] protected float m_knotSize = 2f;
        [SerializeField] protected bool m_renderBoundingBoxes;
        [SerializeField] protected SingleSplineRenderQualitySettings m_centreSpline = new SingleSplineRenderQualitySettings();

        // HLSplines.Runtime original 0x0600021c..0x06000224.
        public SingleSplineRenderQualitySettings CentreSpline => m_centreSpline;
        public int SplineDrawDistance => m_splineDrawDistance;
        public bool IsSelected => m_isSelected;
        public bool DisplayKnotHandles => m_displayKnotHandles;
        public Color KnotHeadColour => m_knotHeadColour;
        public Color KnotTailColour => m_knotTailColour;
        public float KnotSize => m_knotSize;
        public bool RenderBoundingBoxes => m_renderBoundingBoxes;
    }
}
