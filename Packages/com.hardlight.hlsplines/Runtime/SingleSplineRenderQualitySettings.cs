using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public class SingleSplineRenderQualitySettings
    {
        [SerializeField] protected int m_renderQuality = 8;
        [SerializeField] protected float m_lineThickness = 1f;
        [SerializeField] protected bool m_displaySplineControlCage;
        [SerializeField] protected float m_normalLength = 1f;
        [SerializeField] protected float m_normalThickness = 1f;
        [Range(0f, 10f), SerializeField] protected int m_normalsPerKnot = 2;
        [SerializeField] protected Color m_normalColour = Color.cyan;
        [SerializeField] protected float m_maxArrowSize = 2f;
        [SerializeField] protected Color m_displayColour = Color.yellow;

        // HLSplines.Runtime original 0x06000200..0x06000209.
        public int RenderQuality => m_renderQuality;
        public float LineThickness => m_lineThickness;
        public bool DisplaySplineControlCage => m_displaySplineControlCage;
        public float NormalLength => m_normalLength;
        public float NormalThickness => m_normalThickness;
        public int NormalsPerKnot => m_normalsPerKnot;
        public Color NormalColour => m_normalColour;
        public float MaxArrowSize => m_maxArrowSize;
        public Color DisplayColour => m_displayColour;
    }
}
