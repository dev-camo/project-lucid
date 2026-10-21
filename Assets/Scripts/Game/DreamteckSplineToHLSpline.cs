using Dreamteck.Splines;
using Hardlight;
using Spline = Hardlight.Spline; // Dreamteck.Splines also declares Spline.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a02 (1.10.1). Holds the Dreamteck spline computers a Hardlight
    // spline or ribbon is built from.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DreamteckSplineToHLSpline : MonoBehaviour
    {
        [SerializeField] private Spline m_splineComponent;
        [SerializeField] private Ribbon m_ribbonComponent;
        [SerializeField, Min(0.01f)] private float m_baseRibbonWidth = 1f;
        [SerializeField, Tooltip("Offset will be applied to each knot, relative to the dreamteck sample rotation.")]
        private Vector3 m_localOffset;
        [SerializeField, Header("Splines must be in position order.")]
        private SplineComputer[] m_splineComputers;
        [HideInInspector] public float LastUsedBaseRibbonWidth;

        public SplineComputer[] SplineComputers => m_splineComputers; // 06003970
        public float BaseRibbonWidth => m_baseRibbonWidth; // 06003971
        public Vector3 LocalOffset => m_localOffset; // 06003972

        // 06003973 (RVA 0x6B08E0)
        public void Initialise(Spline spline, SplineComputer[] splineComputers)
        {
            m_splineComputers = splineComputers;
            m_splineComponent = spline;
        }

        // 06003974 (RVA 0x6B0920): the width is also recorded as the last one used.
        public void Initialise(Ribbon ribbon, SplineComputer[] splineComputers, float baseWidth)
        {
            m_splineComputers = splineComputers;
            m_ribbonComponent = ribbon;
            m_baseRibbonWidth = LastUsedBaseRibbonWidth = baseWidth;
        }
    }
}
