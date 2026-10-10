// Private genuine original source candidate; rendering and connected service graph are unapproved.
using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FullscreenShaderManager : TimeScaledComponent_SDT, ISystem
    {
        [SerializeField]
        private FullscreenShaderParametersType m_baseParametersType;
        private UnityEngine.Material m_fullscreenShaderMaterial;
        private FullScreenPassRendererFeature m_dreamVignetteFeature;
        private UnityEngine.Rendering.Universal.ScriptableRendererFeature m_uiBlurFeature;
        private const System.String ShaderPropertyName_FadeColour = "_Fade_To_Color";
        private const System.String ShaderPropertyName_FadeAmount = "_Fade_To_Colour_Amount";
        private const System.String ShaderPropertyName_VignetteTexture = "_Vignette_Texture";
        private const System.String ShaderPropertyName_VignetteUVScale = "_Vignette_UV_Scale";
        private const System.String ShaderPropertyName_VignetteAmount = "_Vignette_Amount";
        private const System.String ShaderPropertyName_VignetteOpacity = "_Vignette_Opacity";
        private const System.String ShaderPropertyName_VignetteTint = "_Vignette_Tint";
        private const System.String ShaderPropertyName_VignetteColourBoost = "_Vignette_Colour_Boost";
        private const System.String ShaderPropertyName_FlowMapStrength = "_Vignette_Flow_Map_Strength";
        private const System.String ShaderPropertyName_FlowMapSpeed = "_Vignette_Flow_Map_Speed";
        private const System.String ShaderPropertyName_EdgeDistortionAmount = "_Edge_Distortion_Amount";
        private const System.String ShaderPropertyName_SpeedLinesTexture = "_Speed_Lines_Packed_Noise_Texture";
        private const System.String ShaderPropertyName_SpeedLinesOpacity = "_Speed_Lines_Opacity";
        private const System.String ShaderPropertyName_SpeedLinesAmount = "_Speed_Lines_Amount";
        private const System.String ShaderPropertyName_SpeedLinesScale = "_Speed_Lines_Scale";
        private const System.String ShaderPropertyName_SpeedLinesSpeed = "_Speed_Lines_Speed";
        private const System.String ShaderPropertyName_SpeedLinesColourTint = "_Speed_Lines_Colour_Tint";
        private System.Int32 ShaderPropertyID_FadeColour = Shader.PropertyToID(ShaderPropertyName_FadeColour);
        private System.Int32 ShaderPropertyID_FadeAmount = Shader.PropertyToID(ShaderPropertyName_FadeAmount);
        private System.Int32 ShaderPropertyID_VignetteUVScale = Shader.PropertyToID(ShaderPropertyName_VignetteUVScale);
        private System.Int32 ShaderPropertID_VignetteTexture = Shader.PropertyToID(ShaderPropertyName_VignetteTexture);
        private System.Int32 ShaderPropertyID_VignetteAmount = Shader.PropertyToID(ShaderPropertyName_VignetteAmount);
        private System.Int32 ShaderPropertyID_VignetteOpacity = Shader.PropertyToID(ShaderPropertyName_VignetteOpacity);
        private System.Int32 ShaderPropertyID_VignetteTint = Shader.PropertyToID(ShaderPropertyName_VignetteTint);
        private System.Int32 ShaderPropertyID_VignetteColourBoost = Shader.PropertyToID(ShaderPropertyName_VignetteColourBoost);
        private System.Int32 ShaderPropertyID_FlowMapStrength = Shader.PropertyToID(ShaderPropertyName_FlowMapStrength);
        private System.Int32 ShaderPropertyID_FlowMapSpeed = Shader.PropertyToID(ShaderPropertyName_FlowMapSpeed);
        private System.Int32 ShaderPropertyID_EdgeDistortionAmount = Shader.PropertyToID(ShaderPropertyName_EdgeDistortionAmount);
        private System.Int32 ShaderPropertyID_SpeedLinesTexture = Shader.PropertyToID(ShaderPropertyName_SpeedLinesTexture);
        private System.Int32 ShaderPropertyID_SpeedLinesOpacity = Shader.PropertyToID(ShaderPropertyName_SpeedLinesOpacity);
        private System.Int32 ShaderPropertyID_SpeedLinesAmount = Shader.PropertyToID(ShaderPropertyName_SpeedLinesAmount);
        private System.Int32 ShaderPropertyID_SpeedLinesScale = Shader.PropertyToID(ShaderPropertyName_SpeedLinesScale);
        private System.Int32 ShaderPropertyID_SpeedLinesSpeed = Shader.PropertyToID(ShaderPropertyName_SpeedLinesSpeed);
        private System.Int32 ShaderPropertyID_SpeedLinesColourTint = Shader.PropertyToID(ShaderPropertyName_SpeedLinesColourTint);
        private CollectedFullscreenShaderParameters m_baseParameters;
        private readonly System.Collections.Generic.List<ActiveParameters> m_activeParameters = new List<ActiveParameters>();
        private UnityEngine.Pool.ObjectPool<ActiveParameters> m_activeParametersPool;
        private readonly System.Collections.Generic.Dictionary<ActiveParameters, System.Int32> m_activeParametersIDs = new Dictionary<ActiveParameters, int>();
        private DataManager m_dataManager;
        private readonly Hardlight.StackableData m_fullscreenEffectRequests = new StackableData();
        private readonly System.Collections.Generic.List<ParametersHandle> m_delayedStops = new List<ParametersHandle>();
        private const System.Int32 UIBlurStackableID = 0;
        private const System.Int32 GameRendererIndex = 0;
        private const System.Int32 UIRendererIndex = 1;

        // Original 060035bf; renderer/material setup precedes pool and service callbacks.
        protected override void Awake()
        {
            base.Awake();
            ProcessManager.RegisterSystem(this);
            m_dreamVignetteFeature = GetRendererFeature<FullScreenPassRendererFeature>(GameRendererIndex);
            m_fullscreenShaderMaterial = m_dreamVignetteFeature.passMaterial;
            m_uiBlurFeature = GetRendererFeature<UIBlurRenderFeature>(UIRendererIndex);
            m_activeParametersPool = new UnityEngine.Pool.ObjectPool<ActiveParameters>(
                () =>
                {
                    var parameters = new ActiveParameters();
                    m_activeParametersIDs[parameters] = 0;
                    return parameters;
                },
                null,
                parameters =>
                {
                    parameters.Deinitialise();
                    m_activeParametersIDs[parameters] = m_activeParametersIDs[parameters] + 1;
                },
                parameters => { m_activeParametersIDs.Remove(parameters); },
                true, 5, 10000);
            ProcessManager.GetSystemRef<DataManager>().InvokeOnValid(OnDataManagerValid);
            ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerValid);
            m_fullscreenEffectRequests.OnDataUpdated += UpdateFullscreenEffect;
        }

        // Original 060035c0; texture members are deliberately not assigned here.
        private void OnDataManagerValid(DataManager dataManager)
        {
            m_dataManager = dataManager;
            if (m_dataManager.FullscreenShaderParametersDefinitions.TryGetValue(m_baseParametersType,
                out FullscreenShaderParametersDefinition definition))
            {
                m_baseParameters.FadeColour = definition.FadeColour.Evaluate(default(Color), 0f, false);
                m_baseParameters.FadeAmount = definition.FadeAmount.Evaluate(0f, 0f, false);
                m_baseParameters.VignetteAmount = definition.VignetteAmount.Evaluate(0f, 0f, false);
                m_baseParameters.VignetteUVScale = definition.VignetteUVScale.Evaluate(0f, 0f, false);
                m_baseParameters.VignetteOpacity = definition.VignetteOpacity.Evaluate(0f, 0f, false);
                m_baseParameters.VignetteTint = definition.VignetteTint.Evaluate(default(Color), 0f, false);
                m_baseParameters.VignetteColourBoost = definition.VignetteColourBoost.Evaluate(0f, 0f, false);
                m_baseParameters.FlowMapStrength = definition.FlowMapStrength.Evaluate(0f, 0f, false);
                m_baseParameters.FlowMapSpeed = definition.FlowMapSpeed.Evaluate(0f, 0f, false);
                m_baseParameters.EdgeDistortionAmount = definition.EdgeDistortionAmount.Evaluate(0f, 0f, false);
                m_baseParameters.SpeedLinesOpacity = definition.SpeedLinesOpacity.Evaluate(0f, 0f, false);
                m_baseParameters.SpeedLinesAmount = definition.SpeedLinesAmount.Evaluate(0f, 0f, false);
                m_baseParameters.SpeedLinesScale = definition.SpeedLinesScale.Evaluate(0f, 0f, false);
                m_baseParameters.SpeedLinesSpeed = definition.SpeedLinesSpeed.Evaluate(0f, 0f, false);
                m_baseParameters.SpeedLinesColourTint = definition.SpeedLinesColourTint.Evaluate(default(Color), 0f, false);
                ApplyParameters(m_baseParameters);
            }
        }

        // Original 060035c1; no callback swallowing or defensive teardown substitution.
        public override void OnDestroy()
        {
            ProcessManager.UnregisterSystem(this);
            StopAllEffectOverrides();
            m_activeParametersPool.Dispose();
            m_dataManager = null;
            SystemRef<LevelManager> levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
            if (levelManagerRef.IsValid())
                levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
            base.OnDestroy();
        }

        // Original 060035c2; repeatedly stop index zero, then clear delayed handles and apply base.
        public void StopAllEffectOverrides()
        {
            while (m_activeParameters.Count > 0)
            {
                ActiveParameters parameters = m_activeParameters[0];
                StopEffectOverride(new ParametersHandle(parameters, m_activeParametersIDs[parameters]));
            }
            m_delayedStops.Clear();
            ApplyParameters(m_baseParameters);
        }

        // Original 060035c3; setter order is the original shader-property order.
        private void ApplyParameters(CollectedFullscreenShaderParameters baseParameters)
        {
            m_fullscreenShaderMaterial.SetColor(ShaderPropertyID_FadeColour, baseParameters.FadeColour);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_FadeAmount, baseParameters.FadeAmount);
            m_fullscreenShaderMaterial.SetTexture(ShaderPropertID_VignetteTexture, baseParameters.VignetteTexture);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_VignetteUVScale, baseParameters.VignetteUVScale);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_VignetteAmount, baseParameters.VignetteAmount);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_VignetteOpacity, baseParameters.VignetteOpacity);
            m_fullscreenShaderMaterial.SetColor(ShaderPropertyID_VignetteTint, baseParameters.VignetteTint);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_VignetteColourBoost, baseParameters.VignetteColourBoost);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_FlowMapStrength, baseParameters.FlowMapStrength);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_FlowMapSpeed, baseParameters.FlowMapSpeed);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_EdgeDistortionAmount, baseParameters.EdgeDistortionAmount);
            m_fullscreenShaderMaterial.SetTexture(ShaderPropertyID_SpeedLinesTexture, baseParameters.SpeedLinesTexture);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_SpeedLinesOpacity, baseParameters.SpeedLinesOpacity);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_SpeedLinesAmount, baseParameters.SpeedLinesAmount);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_SpeedLinesScale, baseParameters.SpeedLinesScale);
            m_fullscreenShaderMaterial.SetFloat(ShaderPropertyID_SpeedLinesSpeed, baseParameters.SpeedLinesSpeed);
            m_fullscreenShaderMaterial.SetColor(ShaderPropertyID_SpeedLinesColourTint, baseParameters.SpeedLinesColourTint);
        }

        // Original 060035c4; supplied delta is used only for delayed stops.
        protected override void InternalUpdate(float deltaTime)
        {
            bool effectsActive = m_activeParameters.Count > 0;
            if (effectsActive != m_dreamVignetteFeature.isActive)
                m_dreamVignetteFeature.SetActive(effectsActive);
            if (m_activeParameters.Count == 0) return;
            if (deltaTime > 0f)
            {
                for (int i = 0; i < m_delayedStops.Count; i++)
                {
                    ParametersHandle handle = m_delayedStops[i];
                    handle.DelayedStopTimeRemaining -= deltaTime;
                    if (handle.DelayedStopTimeRemaining <= 0f)
                    {
                        StopEffectOverride(handle);
                        m_delayedStops.RemoveAt(i);
                        i--;
                    }
                    else m_delayedStops[i] = handle;
                }
            }
            CollectedFullscreenShaderParameters parameters = m_baseParameters;
            for (int i = 0; i < m_activeParameters.Count; i++)
            {
                ActiveParameters active = m_activeParameters[i];
                active.ElapsedSeconds += Time.deltaTime;
                if (!active.UntilStopped && active.ElapsedSeconds >= active.Duration)
                {
                    m_activeParameters.RemoveAt(i);
                    active.OnFinished?.Invoke();
                    m_activeParametersPool.Release(active);
                    i--;
                    continue;
                }
                float t = active.ElapsedSeconds;
                if (active.ApplyCurveOverDuration) t /= active.Duration;
                FullscreenShaderParametersDefinition definition = active.Definition;
                CollectParameter(ref parameters.FadeColour, definition.FadeColour, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.FadeAmount, definition.FadeAmount, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteTexture, definition.VignetteTexture, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteUVScale, definition.VignetteUVScale, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteAmount, definition.VignetteAmount, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteOpacity, definition.VignetteOpacity, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteTint, definition.VignetteTint, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.VignetteColourBoost, definition.VignetteColourBoost, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.FlowMapStrength, definition.FlowMapStrength, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.FlowMapSpeed, definition.FlowMapSpeed, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.EdgeDistortionAmount, definition.EdgeDistortionAmount, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesTexture, definition.SpeedLinesTexture, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesOpacity, definition.SpeedLinesOpacity, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesAmount, definition.SpeedLinesAmount, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesScale, definition.SpeedLinesScale, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesSpeed, definition.SpeedLinesSpeed, t, active.ApplyCurveOverDuration);
                CollectParameter(ref parameters.SpeedLinesColourTint, definition.SpeedLinesColourTint, t, active.ApplyCurveOverDuration);
            }
            ApplyParameters(parameters);
        }

        // Original 060035c5; each original generic definition is counted once.
        private void CollectParameter<T>(ref T parameter,
            FullscreenShaderParametersDefinition.FullscreenShaderParameter<T> parameterDefinition,
            float t, bool normaliseDuration)
        {
            parameter = parameterDefinition.Evaluate(parameter, t, normaliseDuration);
        }

        // Original 060035c6; equal priority follows existing entries and update(0) is virtual.
        public bool StartEffectOverride(FullscreenShaderParametersType type, out ParametersHandle parametersHandle,
            float duration = 0f, bool applyCurveOverDuration = false, bool untilStopped = false,
            Action onFinished = null)
        {
            if (m_dataManager.FullscreenShaderParametersDefinitions.TryGetValue(type,
                out FullscreenShaderParametersDefinition definition))
            {
                ActiveParameters parameters = m_activeParametersPool.Get();
                parameters.Initialise(definition, duration, applyCurveOverDuration, untilStopped, onFinished);
                parametersHandle = new ParametersHandle(parameters, m_activeParametersIDs[parameters]);
                int count = m_activeParameters.Count;
                for (int i = 0; i <= count; i++)
                {
                    if (i == count)
                    {
                        m_activeParameters.Add(parameters);
                        break;
                    }
                    if (m_activeParameters[i].Definition.Priority > definition.Priority)
                    {
                        m_activeParameters.Insert(i, parameters);
                        break;
                    }
                }
                InternalUpdate(0f);
                return true;
            }
            parametersHandle = default(ParametersHandle);
            return false;
        }

        // Original 060035c7; no validation before appending this value handle.
        public void StopEffectOverrideWithDelay(ParametersHandle parametersHandle, float delayStopSeconds)
        {
            parametersHandle.DelayedStopTimeRemaining = delayStopSeconds;
            m_delayedStops.Add(parametersHandle);
        }

        // Original 060035c8; valid ID permits callback/release even if List.Remove returned false.
        public void StopEffectOverride(ParametersHandle parametersHandle)
        {
            if (m_activeParametersIDs.TryGetValue(parametersHandle.ActiveParameters, out int id)
                && id == parametersHandle.InstanceID)
            {
                m_activeParameters.Remove(parametersHandle.ActiveParameters);
                parametersHandle.ActiveParameters.OnFinished?.Invoke();
                m_activeParametersPool.Release(parametersHandle.ActiveParameters);
            }
        }

        // Original 060035c9; update always disables blur for this original ID.
        private void UpdateFullscreenEffect(int stackableDataID)
        {
            if (stackableDataID == UIBlurStackableID) m_uiBlurFeature.SetActive(false);
        }
        public StackableDataHandle RequestUIBlurActive() => m_fullscreenEffectRequests.AddOverride(UIBlurStackableID, true);
        public void CancelFullscreenEffectRequest(StackableDataHandle handle) => m_fullscreenEffectRequests.RemoveOverrides(handle);

        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel) { StopAllEffectOverrides(); }

        // Original 060035ce; exact GetType equality, no derived-feature acceptance or null guards.
        public static T GetRendererFeature<T>(int rendererIndex) where T : ScriptableRendererFeature
        {
            UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            ScriptableRenderer renderer = pipeline.GetRenderer(rendererIndex);
            List<ScriptableRendererFeature> features = typeof(ScriptableRenderer)
                .GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(renderer) as List<ScriptableRendererFeature>;
            for (int i = 0; i < features.Count; i++)
                if (features[i].GetType() == typeof(T)) return (T)features[i];
            return null;
        }
        // Original 060035cf; only upper bound is tested, a negative index still reaches List access.
        public static ScriptableRendererFeature GetRendererFeature(int rendererIndex, int rendererFeatureIndex)
        {
            UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            ScriptableRenderer renderer = pipeline.GetRenderer(rendererIndex);
            List<ScriptableRendererFeature> features = typeof(ScriptableRenderer)
                .GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(renderer) as List<ScriptableRendererFeature>;
            if (rendererFeatureIndex < features.Count) return features[rendererFeatureIndex];
            return null;
        }
        private void SetupDebugMenu() { }
        private void RemoveDebugMenu() { }
        // Original 060035d2: the field initializers precede the genuine base constructor.
        public FullscreenShaderManager() { }

        private struct CollectedFullscreenShaderParameters
        {
            public UnityEngine.Color FadeColour;
            public System.Single FadeAmount;
            public UnityEngine.Texture VignetteTexture;
            public System.Single VignetteUVScale;
            public System.Single VignetteAmount;
            public System.Single VignetteOpacity;
            public UnityEngine.Color VignetteTint;
            public System.Single VignetteColourBoost;
            public System.Single FlowMapStrength;
            public System.Single FlowMapSpeed;
            public System.Single EdgeDistortionAmount;
            public UnityEngine.Texture SpeedLinesTexture;
            public System.Single SpeedLinesOpacity;
            public System.Single SpeedLinesAmount;
            public System.Single SpeedLinesScale;
            public System.Single SpeedLinesSpeed;
            public UnityEngine.Color SpeedLinesColourTint;
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class ActiveParameters
        {
            public FullscreenShaderParametersDefinition Definition { get; private set; }
            public float Duration { get; private set; }
            public bool ApplyCurveOverDuration { get; private set; }
            public float ElapsedSeconds { get; set; }
            public bool UntilStopped { get; private set; }
            public Action OnFinished { get; private set; }
            public void Initialise(FullscreenShaderParametersDefinition definition, float duration,
                bool applyCurveOverDuration, bool untilStopped, Action onFinished)
            {
                Definition = definition;
                Duration = duration;
                ApplyCurveOverDuration = applyCurveOverDuration;
                ElapsedSeconds = 0f;
                UntilStopped = untilStopped;
                OnFinished = onFinished;
            }
            // Original retains all scalar state; only these two references are reset.
            public void Deinitialise() { Definition = null; OnFinished = null; }
            public ActiveParameters() { }
        }

        public struct ParametersHandle
        {
            public ActiveParameters ActiveParameters { get; }
            public int InstanceID { get; }
            public float DelayedStopTimeRemaining { get; set; }
            public ParametersHandle(ActiveParameters activeParameters, int instanceID)
            {
                ActiveParameters = activeParameters;
                InstanceID = instanceID;
                DelayedStopTimeRemaining = 0f;
            }
        }
    }
}
