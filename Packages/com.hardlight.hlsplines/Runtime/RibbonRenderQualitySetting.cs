using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public class RibbonRenderQualitySetting : SplineRenderQualitySetting
    {
        [SerializeField] private bool m_showLinesAtKnots = true;
        [SerializeField] protected SingleSplineRenderQualitySettings m_leftSplineQuality = new SingleSplineRenderQualitySettings();
        [SerializeField] protected SingleSplineRenderQualitySettings m_rightSplineQuality = new SingleSplineRenderQualitySettings();

        // HLSplines.Runtime original 0x060001fc..0x060001ff.
        public SingleSplineRenderQualitySettings LeftSpline => m_leftSplineQuality;
        public SingleSplineRenderQualitySettings RightSpline => m_rightSplineQuality;
        public bool ShowLinesAtKnots => m_showLinesAtKnots;
    }
}
