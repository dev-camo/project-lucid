using System.Collections.Generic;
using Cinemachine;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.CharCoreShared
{
    // Test-only concrete CinemachineParametersBlender (an abstract MonoBehaviour). It supplies the two
    // abstract members, exposes the protected members a case drives, and can record what the shipped
    // PrePipelineMutateCameraStateCallback hands to its virtual EvaluateBlendCurve and Evaluate. Editor-only
    // assemblies cannot supply attachable components, so it lives in this runtime test assembly.
    public sealed class CinemachineParametersBlenderProbe : CinemachineParametersBlender
    {
        private readonly BlendParameters m_supplied = new BlendParameters();

        public int InitialiseCalls;
        public bool Record;
        public float CurveResult = 0.5f;
        public readonly List<float> CurveDeltaTimes = new List<float>();
        public readonly List<float> EvaluatedBlendValues = new List<float>();

        protected override void InitialiseCinemachineComponents() => InitialiseCalls++;

        protected override BlendParameters GetBlendParameters() => m_supplied;

        protected override float EvaluateBlendCurve(float deltaTime)
        {
            if (!Record) return base.EvaluateBlendCurve(deltaTime);
            CurveDeltaTimes.Add(deltaTime);
            return CurveResult;
        }

        protected override void Evaluate(ref CameraState state, float blendValue)
        {
            if (!Record)
            {
                base.Evaluate(ref state, blendValue);
                return;
            }
            EvaluatedBlendValues.Add(blendValue);
        }

        // Authors one float parameter of the supplied BlendParameters.
        public void SetFloat(string which, bool active, float negativeEnd, float start, float positiveEnd)
        {
            var parameter = new Parameter<float> { Active = active, NegativeEndValue = negativeEnd, StartValue = start, PositiveEndValue = positiveEnd };
            switch (which)
            {
                case "FOV": m_supplied.FOV = parameter; break;
                case "YDamping": m_supplied.YDamping = parameter; break;
                case "ZDamping": m_supplied.ZDamping = parameter; break;
                case "YawDamping": m_supplied.YawDamping = parameter; break;
                case "DutchAngle": m_supplied.DutchAngle = parameter; break;
                case "LookaheadTime": m_supplied.LookaheadTime = parameter; break;
                default: throw new System.ArgumentException(which);
            }
        }

        public void SetAimTrackedOffset(bool active, Vector3 negativeEnd, Vector3 start, Vector3 positiveEnd) =>
            m_supplied.AimTrackedOffset = new Parameter<Vector3>
            {
                Active = active, NegativeEndValue = negativeEnd, StartValue = start, PositiveEndValue = positiveEnd
            };

        public void UseComponents(CinemachineTransposer[] transposers, CinemachineComposer[] composers)
        {
            m_transposers = transposers;
            m_composers = composers;
        }

        public float RunBlendFloat(bool active, float negativeEnd, float start, float positiveEnd, float blendValue) =>
            BlendParameter(new Parameter<float> { Active = active, NegativeEndValue = negativeEnd, StartValue = start, PositiveEndValue = positiveEnd }, blendValue);

        public Vector3 RunBlendVector(bool active, Vector3 negativeEnd, Vector3 start, Vector3 positiveEnd, float blendValue) =>
            BlendParameter(new Parameter<Vector3> { Active = active, NegativeEndValue = negativeEnd, StartValue = start, PositiveEndValue = positiveEnd }, blendValue);

        public float RunLerpWithNegative(float startValue, float positiveEndValue, float negativeEndValue, float blendValue) =>
            LerpWithNegative(startValue, positiveEndValue, negativeEndValue, blendValue);

        public void RunEvaluate(ref CameraState state, float blendValue) => Evaluate(ref state, blendValue);

        public float RunEvaluateBlendCurve(float deltaTime) => EvaluateBlendCurve(deltaTime);

        public float RunGetBlendSourceValue() => GetBlendSourceValue();

        public void RunAwake() => Awake();
    }
}
