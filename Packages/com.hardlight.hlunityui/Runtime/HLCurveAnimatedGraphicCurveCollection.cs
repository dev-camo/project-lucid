// Original HLUnityUI.Runtime HLCurveAnimatedGraphicCurveCollection; complete shipping scope and both CPUs preserved.
// Source spelling/helper inlining is inferred. Native/provider faults, exact float/NaN/signed-zero,
// Unity lifetime/serialization, current emission and gameplay equivalence require separate qualification.
// 0x06000423 get_OnPressedXCurve arm64 0x1b6320c..0x1b63214; x86_64 0x1b5ab70..0x1b5ab80.
// 0x06000424 get_OnPressedYCurve arm64 0x1b63214..0x1b6321c; x86_64 0x1b5ab80..0x1b5ab90.
// 0x06000425 get_OnReleasedXCurve arm64 0x1b6321c..0x1b63224; x86_64 0x1b5ab90..0x1b5aba0.
// 0x06000426 get_OnReleasedYCurve arm64 0x1b63224..0x1b6322c; x86_64 0x1b5aba0..0x1b5abb0.
// 0x06000427 get_OnDisabledXCurve arm64 0x1b6322c..0x1b63234; x86_64 0x1b5abb0..0x1b5abc0.
// 0x06000428 get_OnDisabledYCurve arm64 0x1b63234..0x1b6323c; x86_64 0x1b5abc0..0x1b5abd0.
// 0x06000429 get_OnDisabledAnimDirection arm64 0x1b6323c..0x1b63244; x86_64 0x1b5abd0..0x1b5abe0.
// 0x0600042a .ctor arm64 0x1b63244..0x1b632a0; x86_64 0x1b5abe0..0x1b5ac30.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "NewCurveAnimatedGraphicCurveCollectionAsset", menuName = "Hardlight/CurveAnimatedGraphic Curve Collection Asset", order = 41)]
    public class HLCurveAnimatedGraphicCurveCollection : ScriptableObject
    {
        [SerializeField] private HLCurve m_onPressedXCurve;
        [SerializeField] private HLCurve m_onPressedYCurve;
        [SerializeField] private HLCurve m_onReleasedXCurve;
        [SerializeField] private HLCurve m_onReleasedYCurve;
        [SerializeField] private HLCurve m_onDisabledXCurve;
        [SerializeField] private HLCurve m_onDisabledYCurve;
        [SerializeField] private Vector2 m_onDisabledAnimDirection = Vector2.zero;

        public HLCurve OnPressedXCurve { get { return m_onPressedXCurve; } }
        public HLCurve OnPressedYCurve { get { return m_onPressedYCurve; } }
        public HLCurve OnReleasedXCurve { get { return m_onReleasedXCurve; } }
        public HLCurve OnReleasedYCurve { get { return m_onReleasedYCurve; } }
        public HLCurve OnDisabledXCurve { get { return m_onDisabledXCurve; } }
        public HLCurve OnDisabledYCurve { get { return m_onDisabledYCurve; } }
        public Vector2 OnDisabledAnimDirection { get { return m_onDisabledAnimDirection; } }
    }
}
