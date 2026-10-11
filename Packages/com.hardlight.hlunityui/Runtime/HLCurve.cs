// Original HLUnityUI.Runtime HLCurve; complete shipping scope and both CPUs preserved.
// Source spelling/helper inlining is inferred. Native/provider faults, exact float/NaN/signed-zero,
// Unity lifetime/serialization, current emission and gameplay equivalence require separate qualification.
// 0x06000418 get_Curve arm64 0x1b62eb0..0x1b62eb8; x86_64 0x1b5a870..0x1b5a880.
// 0x06000419 EndTime arm64 0x1b5b9d4..0x1b5ba4c; x86_64 0x1b53820..0x1b53880.
// 0x0600041a SampleAtTime arm64 0x1b5ba4c..0x1b5ba58; x86_64 0x1b53880..0x1b53890.
// 0x0600041b SampleNormalised arm64 0x1b62f34..0x1b62fd4; x86_64 0x1b5a8f0..0x1b5a980.
// 0x0600041c GetCurveEndTime arm64 0x1b62eb8..0x1b62f2c; x86_64 0x1b5a880..0x1b5a8e0.
// 0x0600041d GetCurveEndTime arm64 0x1b63070..0x1b630e8; x86_64 0x1b5aa10..0x1b5aa70.
// 0x0600041e SampleCurveAtTime arm64 0x1b62f2c..0x1b62f34; x86_64 0x1b5a8e0..0x1b5a8f0.
// 0x0600041f SampleCurveAtTime arm64 0x1b630e8..0x1b630f4; x86_64 0x1b5aa70..0x1b5aa80.
// 0x06000420 SampleCurveNormalised arm64 0x1b62fd4..0x1b63070; x86_64 0x1b5a980..0x1b5aa10.
// 0x06000421 SampleCurveNormalised arm64 0x1b630f4..0x1b63194; x86_64 0x1b5aa80..0x1b5ab10.
// 0x06000422 .ctor arm64 0x1b63194..0x1b6320c; x86_64 0x1b5ab10..0x1b5ab70.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "NewCurveAsset", menuName = "Hardlight/Curve Asset", order = 40)]
    public sealed class HLCurve : ScriptableObject
    {
        [SerializeField] private AnimationCurve m_curve = new AnimationCurve();

        public AnimationCurve Curve { get { return m_curve; } }

        public float EndTime()
        {
            return GetCurveEndTime(m_curve);
        }

        public float SampleAtTime(float time)
        {
            return SampleCurveAtTime(m_curve, time);
        }

        public float SampleNormalised(float normalisedPercent)
        {
            return SampleCurveNormalised(m_curve, normalisedPercent);
        }

        public static float GetCurveEndTime(AnimationCurve curve)
        {
            int length = curve.length;
            return length == 0 ? 0f : curve[length - 1].time;
        }

        public static float GetCurveEndTime(HLCurve curve)
        {
            return GetCurveEndTime(curve.Curve);
        }

        public static float SampleCurveAtTime(AnimationCurve curve, float time)
        {
            return curve.Evaluate(time);
        }

        public static float SampleCurveAtTime(HLCurve curve, float time)
        {
            return SampleCurveAtTime(curve.Curve, time);
        }

        public static float SampleCurveNormalised(AnimationCurve curve, float normalisedPercent)
        {
            float endTime = GetCurveEndTime(curve);
            return curve.Evaluate(Mathf.Lerp(0f, endTime, normalisedPercent));
        }

        public static float SampleCurveNormalised(HLCurve curve, float normalisedPercent)
        {
            return SampleCurveNormalised(curve.Curve, normalisedPercent);
        }
    }
}
