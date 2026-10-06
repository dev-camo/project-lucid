using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "FullscreenShaderParametersDefinition", menuName = "HardlightProject/DefinitionData/Definitions/FullscreenShaderParametersDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class FullscreenShaderParametersDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersType m_fullscreenShaderType;

        [UnityEngine.Tooltip("Set this to a higher number for these overrides to be applied later on, i.e. they will be more prominent in the weightings.")]
        [UnityEngine.SerializeField]
        private System.Int32 m_priority;

        [UnityEngine.Tooltip("Full screen fade colour.")]
        [UnityEngine.Header("Parameter Overrides")]
        [UnityEngine.Header("Fade To Color")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterColour m_fadeColour;

        [UnityEngine.Tooltip("Between 0 and 1. 1 covers the screen fully in the fade colour.")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_fadeAmount;

        [UnityEngine.Header("Dream Vignette")]
        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Texture to apply.")]
        [UnityEngine.Header("Texture Override")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterTexture m_vignetteTexture;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Scales the UVs of the vignette texture from the centre of the screen.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_vignetteUVScale;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between 0 and 2. Strength of effect.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_vignetteAmount;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between 0 and 1. Opacity of the effect.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_vignetteOpacity;

        [UnityEngine.Tooltip("Vignette colour tint to apply.")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterColour m_vignetteTint;

        [UnityEngine.Tooltip("Between 1 and 2. Values above 1 boost the brightness of the vignette colour.")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_vignetteColourBoost;

        [UnityEngine.Tooltip("Between 0 and 1. Strength of the flow map.")]
        [UnityEngine.Header("Flow Map")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_flowMapStrength;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between 0 and 1. Speed the flow map moves at.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_flowMapSpeed;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between negative and positive infinity. Strength of screen warping/fisheye effect.")]
        [UnityEngine.Header("Edge Distortion")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_edgeDistortionAmount;

        [UnityEngine.Header("Speed Lines")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterTexture m_speedLinesTexture;

        [UnityEngine.Tooltip("Between 0 and 1. Opacity of the speed lines where 1 is fully opaque.")]
        [UnityEngine.SerializeField]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_speedLinesOpacity;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Greater than or equal to 0 with no upper limit. Amount of speed lines.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_speedLinesAmount;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between 0 and 0.2. Scale of speed lines.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_speedLinesScale;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Between -2 and 2. Speed of speed lines.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat m_speedLinesSpeed;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Speed lines colour tint.")]
        private HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterHDRColour m_speedLinesColourTint;

        // Original Game.Runtime 0x06001cd0, ARM 0x523bc4.
        public HardlightProject.FullscreenShaderParametersType FullscreenShaderParametersType => m_fullscreenShaderType;

        // Original Game.Runtime 0x06001cd1, ARM 0x523bcc.
        public System.Int32 Priority => m_priority;

        // Original Game.Runtime 0x06001cd2, ARM 0x523bd4.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterColour FadeColour => m_fadeColour;

        // Original Game.Runtime 0x06001cd3, ARM 0x523bdc.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat FadeAmount => m_fadeAmount;

        // Original Game.Runtime 0x06001cd4, ARM 0x523be4.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterTexture VignetteTexture => m_vignetteTexture;

        // Original Game.Runtime 0x06001cd5, ARM 0x523bec.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat VignetteUVScale => m_vignetteUVScale;

        // Original Game.Runtime 0x06001cd6, ARM 0x523bf4.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat VignetteAmount => m_vignetteAmount;

        // Original Game.Runtime 0x06001cd7, ARM 0x523bfc.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat VignetteOpacity => m_vignetteOpacity;

        // Original Game.Runtime 0x06001cd8, ARM 0x523c04.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterColour VignetteTint => m_vignetteTint;

        // Original Game.Runtime 0x06001cd9, ARM 0x523c0c.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat VignetteColourBoost => m_vignetteColourBoost;

        // Original Game.Runtime 0x06001cda, ARM 0x523c14.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat FlowMapStrength => m_flowMapStrength;

        // Original Game.Runtime 0x06001cdb, ARM 0x523c1c.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat FlowMapSpeed => m_flowMapSpeed;

        // Original Game.Runtime 0x06001cdc, ARM 0x523c24.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat EdgeDistortionAmount => m_edgeDistortionAmount;

        // Original Game.Runtime 0x06001cdd, ARM 0x523c2c.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterTexture SpeedLinesTexture => m_speedLinesTexture;

        // Original Game.Runtime 0x06001cde, ARM 0x523c34.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat SpeedLinesOpacity => m_speedLinesOpacity;

        // Original Game.Runtime 0x06001cdf, ARM 0x523c3c.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat SpeedLinesAmount => m_speedLinesAmount;

        // Original Game.Runtime 0x06001ce0, ARM 0x523c44.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat SpeedLinesScale => m_speedLinesScale;

        // Original Game.Runtime 0x06001ce1, ARM 0x523c4c.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterFloat SpeedLinesSpeed => m_speedLinesSpeed;

        // Original Game.Runtime 0x06001ce2, ARM 0x523c54.
        public HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameterHDRColour SpeedLinesColourTint => m_speedLinesColourTint;

        // Original Game.Runtime 0x06001ce3, ARM 0x523c5c.
        // Preserve original validation order/ranges, including tooltip differences.
        private void OnValidate()
        {
            m_fadeAmount.SetLimits(0f, 1f);
            m_vignetteAmount.SetLimits(0f, 2f);
            m_vignetteUVScale.SetLimits(0f, 2f);
            m_vignetteOpacity.SetLimits(0f, 1f);
            m_vignetteColourBoost.SetLimits(0f, 2f);
            m_flowMapStrength.SetLimits(-1f, 1f);
            m_flowMapSpeed.SetLimits(-1f, 1f);
            m_edgeDistortionAmount.SetLimits(float.NegativeInfinity, float.PositiveInfinity);
            m_speedLinesOpacity.SetLimits(0f, 1f);
            m_speedLinesAmount.SetLimits(0f, float.PositiveInfinity);
            m_speedLinesScale.SetLimits(0f, 0.2f);
            m_speedLinesSpeed.SetLimits(-2f, 2f);
        }

        // Original Game.Runtime 0x06001ce4, ARM 0x523db0.
        // Natural original SO-only constructor; no nested default allocation.

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        public abstract class FullscreenShaderParameter : System.Object
        {
            [UnityEngine.SerializeField]
            protected System.Boolean m_overrideEnabled;

            [UnityEngine.SerializeField]
            [Hardlight.ShowIf("m_overrideEnabled", (string)null)]
            protected UnityEngine.AnimationCurve m_weightOverTime;

            // Original Game.Runtime 0x06001ce5, ARM 0x523db8.
            public bool OverrideEnabled => m_overrideEnabled;

            // Original Game.Runtime 0x06001ce6, ARM 0x523dc0.
            // Natural original base-only constructor; native serialized defaults.
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        public abstract class FullscreenShaderParameter<T> : HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameter
        {
            [UnityEngine.SerializeField]
            [UnityEngine.HideInInspector]
            protected T m_minLimit;

            [UnityEngine.SerializeField]
            [UnityEngine.HideInInspector]
            protected T m_maxLimit;

            // Original 0x06001ce7: genuine protected abstract contract, no native body.
            protected abstract T Lerp(T from, T to, float t);

            // Original 0x06001ce8: genuine protected abstract contract, no native body.
            protected abstract T GetValue();

            // Original 0x06001ce9: genuine protected abstract contract, no native body.
            protected abstract void SetValue(T value);

            // Original Game.Runtime 0x06001cea, ARM 0x1367c50.
            protected virtual T EnforceLimits(T value) => value;

            // Original Game.Runtime 0x06001ceb, ARM 0x1367c54.
            // Both limits precede virtual GetValue/EnforceLimits/SetValue.
            public void SetLimits(T min, T max)
            {
                m_minLimit = min;
                m_maxLimit = max;
                SetValue(EnforceLimits(GetValue()));
            }

            // Original Game.Runtime 0x06001cec, ARM 0x1367ca8.
            // Clamp incoming base first; curve length is read even without normalisation.
            public T Evaluate(T baseValue, float time, bool normaliseDuration)
            {
                T result = EnforceLimits(baseValue);
                if (m_overrideEnabled)
                {
                    int count = m_weightOverTime.length;
                    if (normaliseDuration && count > 0)
                        time *= m_weightOverTime[count - 1].time;
                    float weight = Mathf.Clamp01(m_weightOverTime.Evaluate(time));
                    result = Lerp(result, EnforceLimits(GetValue()), weight);
                }
                return result;
            }

            // Original Game.Runtime 0x06001ced, ARM 0x1367e00.
            // Natural original base-only constructor; native serialized defaults.
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        public sealed class FullscreenShaderParameterFloat : HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameter<System.Single>
        {
            [UnityEngine.SerializeField]
            [Hardlight.ShowIf("m_overrideEnabled", (string)null)]
            private System.Single m_value;

            // Original Game.Runtime 0x06001cee, ARM 0x523dc8.
            protected override System.Single Lerp(System.Single from, System.Single to, float t) => Mathf.Lerp(from, to, t);

            // Original Game.Runtime 0x06001cef, ARM 0x523df0.
            protected override void SetValue(System.Single value) => m_value = value;

            // Original Game.Runtime 0x06001cf0, ARM 0x523df8.
            protected override System.Single GetValue() => m_value;

            // Original Game.Runtime 0x06001cf1, ARM 0x523e00.
            protected override float EnforceLimits(float value) => Mathf.Clamp(value, m_minLimit, m_maxLimit);

            // Original Game.Runtime 0x06001cf2, ARM 0x523e18.
            // Natural original base-only constructor; native serialized defaults.
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        public sealed class FullscreenShaderParameterColour : HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameter<UnityEngine.Color>
        {
            [Hardlight.ShowIf("m_overrideEnabled", (string)null)]
            [UnityEngine.SerializeField]
            private UnityEngine.Color m_value;

            // Original Game.Runtime 0x06001cf3, ARM 0x523e6c.
            protected override UnityEngine.Color Lerp(UnityEngine.Color from, UnityEngine.Color to, float t) => Color.Lerp(from, to, t);

            // Original Game.Runtime 0x06001cf4, ARM 0x523ebc.
            protected override void SetValue(UnityEngine.Color value) => m_value = value;

            // Original Game.Runtime 0x06001cf5, ARM 0x523ec8.
            protected override UnityEngine.Color GetValue() => m_value;

            // Original Game.Runtime 0x06001cf6, ARM 0x523ed4.
            // Natural original base-only constructor; native serialized defaults.
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        public sealed class FullscreenShaderParameterTexture : HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameter<UnityEngine.Texture>
        {
            [Hardlight.ShowIf("m_overrideEnabled", (string)null)]
            [UnityEngine.SerializeField]
            private UnityEngine.Texture m_value;

            // Original Game.Runtime 0x06001cf7, ARM 0x523f28.
            protected override UnityEngine.Texture Lerp(UnityEngine.Texture from, UnityEngine.Texture to, float t) => m_overrideEnabled ? to : from;

            // Original Game.Runtime 0x06001cf8, ARM 0x523f38.
            protected override void SetValue(UnityEngine.Texture value) => m_value = value;

            // Original Game.Runtime 0x06001cf9, ARM 0x523f40.
            protected override UnityEngine.Texture GetValue() => m_value;

            // Original Game.Runtime 0x06001cfa, ARM 0x523f48.
            // Natural original base-only constructor; native serialized defaults.
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        public sealed class FullscreenShaderParameterHDRColour : HardlightProject.FullscreenShaderParametersDefinition.FullscreenShaderParameter<UnityEngine.Color>
        {
            [UnityEngine.SerializeField]
            [Hardlight.ShowIf("m_overrideEnabled", (string)null)]
            [UnityEngine.ColorUsage(true, true)]
            private UnityEngine.Color m_value;

            // Original Game.Runtime 0x06001cfb, ARM 0x523f9c.
            protected override UnityEngine.Color Lerp(UnityEngine.Color from, UnityEngine.Color to, float t) => Color.Lerp(from, to, t);

            // Original Game.Runtime 0x06001cfc, ARM 0x523fec.
            protected override void SetValue(UnityEngine.Color value) => m_value = value;

            // Original Game.Runtime 0x06001cfd, ARM 0x523ff8.
            protected override UnityEngine.Color GetValue() => m_value;

            // Original Game.Runtime 0x06001cfe, ARM 0x524004.
            // Natural original base-only constructor; native serialized defaults.
        }

    }
}
