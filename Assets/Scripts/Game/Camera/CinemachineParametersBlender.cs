using System;
using Cinemachine;
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000279 (BlendParameters 0x0200027a, Parameter`1 0x0200027b).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CinemachineParametersBlender : CinemachineExtension
    {
        [SerializeField]
        private CinemachineParametersBlenderSource m_blendValueSource;
        [SerializeField]
        private AnimationCurve m_blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField]
        private bool m_useDebugBlendValue;
        [SerializeField]
        [Range(0f, 1f)]
        [ShowIf("m_useDebugBlendValue", (string)null)]
        private float m_debugBlendValue;
        protected CinemachineTransposer[] m_transposers;
        protected CinemachineComposer[] m_composers;

        // 0x06000dc8
        protected override void Awake()
        {
            base.Awake();
            InitialiseCinemachineComponents();
        }

        // 0x06000dc9
        protected abstract void InitialiseCinemachineComponents();

        // 0x06000dca
        protected abstract BlendParameters GetBlendParameters();

        // 0x06000dcb: the shipped callback is empty.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime) { }

        // 0x06000dcc: a negative frame time is treated as zero before the curve is sampled.
        public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam,
            ref CameraState curState, float deltaTime)
        {
            base.PrePipelineMutateCameraStateCallback(vcam, ref curState, deltaTime);
            float blendValue = EvaluateBlendCurve(Mathf.Max(0f, deltaTime));
            Evaluate(ref curState, blendValue);
        }

        // 0x06000dcd: the frame time is unused; the curve is sampled at the source's blend value.
        protected virtual float EvaluateBlendCurve(float deltaTime)
        {
            return m_blendCurve.Evaluate(m_blendValueSource.BlendValue);
        }

        // 0x06000dce
        protected float GetBlendSourceValue() => m_blendValueSource.BlendValue;

        // 0x06000dcf: only the active parameters are written to the camera state and components.
        protected virtual void Evaluate(ref CameraState state, float blendValue)
        {
            BlendParameters parameters = GetBlendParameters();
            if (parameters.FOV.Active)
            {
                float fieldOfView = BlendParameter(parameters.FOV, blendValue);
                // A physical camera reads the blended value as a focal length.
                if (state.Lens.IsPhysicalCamera)
                    fieldOfView = Camera.FocalLengthToFieldOfView(Mathf.Max(fieldOfView, 0.0001f), state.Lens.SensorSize.y);
                state.Lens.FieldOfView = fieldOfView;
            }
            float yDamping = BlendParameter(parameters.YDamping, blendValue);
            float zDamping = BlendParameter(parameters.ZDamping, blendValue);
            float yawDamping = BlendParameter(parameters.YawDamping, blendValue);
            for (int i = 0; i < m_transposers.Length; i++)
            {
                CinemachineTransposer transposer = m_transposers[i];
                if (parameters.YDamping.Active) transposer.m_YDamping = yDamping;
                if (parameters.ZDamping.Active) transposer.m_ZDamping = zDamping;
                if (parameters.YawDamping.Active) transposer.m_YawDamping = yawDamping;
            }
            Vector3 aimTrackedOffset = BlendParameter(parameters.AimTrackedOffset, blendValue);
            float lookaheadTime = BlendParameter(parameters.LookaheadTime, blendValue);
            for (int i = 0; i < m_composers.Length; i++)
            {
                CinemachineComposer composer = m_composers[i];
                if (parameters.AimTrackedOffset.Active) composer.m_TrackedObjectOffset = aimTrackedOffset;
                if (parameters.LookaheadTime.Active) composer.m_LookaheadTime = lookaheadTime;
            }
            if (parameters.DutchAngle.Active)
                state.Lens.Dutch = BlendParameter(parameters.DutchAngle, blendValue);
        }

        // 0x06000dd0: an inactive parameter contributes zero.
        protected float BlendParameter(Parameter<float> parameter, float blendValue)
        {
            if (!parameter.Active) return 0f;
            return LerpWithNegative(parameter.StartValue, parameter.PositiveEndValue, parameter.NegativeEndValue, blendValue);
        }

        // 0x06000dd1: the sign of the blend value picks the end point; its magnitude is the lerp factor.
        // The native test sends an unordered blend value to the negative end.
        protected Vector3 BlendParameter(Parameter<Vector3> parameter, float blendValue)
        {
            if (!parameter.Active) return Vector3.zero;
            Vector3 endValue = blendValue >= 0f ? parameter.PositiveEndValue : parameter.NegativeEndValue;
            return Vector3.Lerp(parameter.StartValue, endValue, Mathf.Abs(blendValue));
        }

        // 0x06000dd2
        protected float LerpWithNegative(float startValue, float positiveEndValue, float negativeEndValue, float blendValue)
        {
            float endValue = blendValue >= 0f ? positiveEndValue : negativeEndValue;
            return Mathf.Lerp(startValue, endValue, Mathf.Abs(blendValue));
        }

        // 0x06000dd3: the blend curve initialiser runs before the base constructor.
        protected CinemachineParametersBlender() { }

        // 0x0200027a, ctor 0x06000dd4
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class BlendParameters
        {
            public Parameter<float> FOV;
            public Parameter<float> YDamping;
            public Parameter<float> ZDamping;
            public Parameter<float> YawDamping;
            public Parameter<Vector3> AimTrackedOffset;
            public Parameter<float> DutchAngle;
            public Parameter<float> LookaheadTime;

            public BlendParameters() { }
        }

        // 0x0200027b
        [Serializable]
        protected struct Parameter<T>
        {
            public bool Active;
            [ShowIf("Active", (string)null)]
            public T NegativeEndValue;
            [ShowIf("Active", (string)null)]
            public T StartValue;
            [ShowIf("Active", (string)null)]
            [FormerlySerializedAs("EndValue")]
            public T PositiveEndValue;
        }
    }
}
