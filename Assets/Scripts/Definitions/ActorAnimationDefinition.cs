using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenuAttribute(fileName = "ActorAnimationDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ActorAnimationDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public sealed class ActorAnimationDefinition : ScriptableObject
    {
        [UnityEngine.SerializeField]
        private AnimationParameter[] m_parameters = Array.Empty<AnimationParameter>();
        [UnityEngine.SerializeField]
        private AudioParameter[] m_audioParameters = Array.Empty<AudioParameter>();
        [UnityEngine.SerializeField]
        private PFXParameter[] m_pfxParameters = Array.Empty<PFXParameter>();
        [UnityEngine.SerializeField]
        private PFXParameterLeaveState[] m_pfxParametersLeaveState = Array.Empty<PFXParameterLeaveState>();
        [UnityEngine.SerializeField]
        private FullscreenEffectParameter[] m_fullscreenEffectParameters = Array.Empty<FullscreenEffectParameter>();
        private readonly Dictionary<ActorAnimationType, List<AnimationParameterWrapper>> m_parametersLookup = new Dictionary<ActorAnimationType, List<AnimationParameterWrapper>>(HardlightEnumComparers.ActorAnimationTypeComparer);

        public IReadOnlyList<AudioParameter> Audio => m_audioParameters;
        public IReadOnlyList<PFXParameterBase> PFXParameters => m_pfxParameters;
        public IReadOnlyList<PFXParameterBase> PFXParametersLeaveState => m_pfxParametersLeaveState;
        public IReadOnlyList<FullscreenEffectParameter> FullscreenEffectParameters => m_fullscreenEffectParameters;

        private void OnEnable() => RebuildAnimationParameters();

        private void RebuildAnimationParameters()
        {
            // Original 060019c9 clears before reading the authored array. A malformed
            // entry can leave a partially rebuilt dictionary; it is not skipped.
            m_parametersLookup.Clear();
            foreach (AnimationParameter parameter in m_parameters)
            {
                List<AnimationParameterWrapper> parameters = m_parametersLookup.TryGetOrNew(parameter.AnimationType);
                parameters.Add(parameter.ParameterName);
            }
        }

        public void IterateAnimationHashesForType(ActorAnimationType animationType, Action<int> iterator)
        {
            if (!m_parametersLookup.TryGetValue(animationType, out List<AnimationParameterWrapper> parameters))
                return;
            foreach (AnimationParameterWrapper parameter in parameters)
                if (parameter.TryGetAnimationHash(out int animationHash))
                    iterator(animationHash);
        }

        private void OnValidate()
        {
            int persistentAudioCount = 0;
            foreach (AudioParameter parameter in m_audioParameters)
            {
                if (parameter.Behaviour == AudioParameter.ClipBehaviour.EndOnLeaveState ||
                    parameter.Behaviour == AudioParameter.ClipBehaviour.Looping)
                {
                    ++persistentAudioCount;
                    if (persistentAudioCount >= 2)
                    {
                        HLOutput.LogError(string.Format("Error in {0}: Must not have more than one {1} with behaviour {2} or {3}",
                            name, "AudioParameter", AudioParameter.ClipBehaviour.Looping, AudioParameter.ClipBehaviour.EndOnLeaveState));
                        // The logging callback runs before mutation, as in 060019cb.
                        parameter.Behaviour = AudioParameter.ClipBehaviour.OneShot;
                    }
                }
            }
            RebuildAnimationParameters();
        }

        public ActorAnimationDefinition() { }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        public sealed class AnimationParameter : ISerializationCallbackReceiver
        {
            [UnityEngine.HideInInspector]
            public string Name;
            public ActorAnimationType AnimationType;
            public AnimationParameterWrapper ParameterName;

            public void OnBeforeSerialize() => Name = GetString();
            public void OnAfterDeserialize() => Name = GetString();
            private string GetString() => AnimationType.GetString() + " - " + ParameterName.ParameterName;
            public AnimationParameter() { }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        public sealed class AudioParameter
        {
            public ClipAction onEnter;
            public ClipAction onLeave;
            [Hardlight.Utils.HashEnumAttribute(typeof(ActorAudioTypes))]
            public ActorAudioTypes Clip;
            [UnityEngine.HideInInspector]
            public ClipBehaviour Behaviour = ClipBehaviour.OneShot;
            public BehaviourOfClip BehaviourOn;
            [UnityEngine.TooltipAttribute("Add an optional delay to starting this audio, which will be time scaled. If the animation state ends before the delayed audio starts, it will not play.")]
            public float DelaySeconds;

            public AudioParameter() { }

            public enum ClipBehaviour
            {
                EndOnLeaveState = 0,
                OneShot = 1,
                Looping = 2,
                LoopingUntilExplicitlyStopped = 3,
                EndLoopOnLeave = 4,
                OneShotOnLeaveState = 5,
                None = 6
            }

            public enum BehaviourOfClip
            {
                None = -1,
                OneShot = 0,
                Loop = 1,
                OneShotVO = 2,
                LoopRaw = 3,
                OneShotRaw = 4
            }

            public enum ClipAction
            {
                None = 0,
                Play = 1,
                Stop = 2
            }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        public sealed class PFXParameterCondition
        {
            [UnityEngine.SerializeField]
            private PFXParameterConditionType m_conditionType;
            [UnityEngine.SerializeField]
            private ComparisonType m_comparisonType = ComparisonType.GreaterThanEqual;
            [UnityEngine.SerializeField]
            private float m_criteria;

            public PFXParameterConditionType ConditionType => m_conditionType;
            public ComparisonType ComparisonType => m_comparisonType;
            public float Criteria => m_criteria;
            public PFXParameterCondition() { }

            public enum PFXParameterConditionType
            {
                ActorXZVelocity = 0,
                ActorImpactVelocity = 1,
                BrainMovement = 2
            }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        public abstract class PFXParameterBase
        {
            [UnityEngine.SerializeField]
            [UnityEngine.Serialization.FormerlySerializedAsAttribute("PFXTrigger")]
            private ActorParticleTriggerType m_pfxTrigger;
            [UnityEngine.SerializeField]
            private PFXParameterCondition[] m_conditions;

            public ActorParticleTriggerType PFXTrigger => m_pfxTrigger;
            public IReadOnlyCollection<PFXParameterCondition> Conditions => m_conditions;
            public abstract bool EvaluateContinuously { get; }
            public abstract bool EndOnLeaveState { get; }
            public abstract bool RemoveEmittedParticles { get; }
            public abstract float RemoveDelaySeconds { get; }
            protected PFXParameterBase() { }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        public sealed class PFXParameter : PFXParameterBase
        {
            [UnityEngine.TooltipAttribute("If ticked, evaluate conditions every frame to start and stop pfx.")]
            [UnityEngine.SerializeField]
            private bool m_evaluateContinuously;
            [UnityEngine.SerializeField]
            [UnityEngine.Serialization.FormerlySerializedAsAttribute("EndOnLeaveState")]
            private bool m_endOnLeaveState = true;
            [Hardlight.ShowIfAttribute("m_endOnLeaveState", (string)null)]
            [UnityEngine.TooltipAttribute("If ticked, already emitted particles will be removed on leaving the state, rather than being left to expire naturally.")]
            [UnityEngine.SerializeField]
            private bool m_removeEmittedParticles;
            [Hardlight.ShowIfAttribute("m_endOnLeaveState", (string)null)]
            [UnityEngine.TooltipAttribute("Delay removing the particle for this many seconds after leaving the state.")]
            [UnityEngine.SerializeField]
            private float m_removeDelaySeconds;

            public override bool EvaluateContinuously => m_evaluateContinuously;
            public override bool EndOnLeaveState => m_endOnLeaveState;
            // Original 060019df gates removal, but the delay getter remains ungated.
            public override bool RemoveEmittedParticles => m_removeEmittedParticles && m_endOnLeaveState;
            public override float RemoveDelaySeconds => m_removeDelaySeconds;
            public PFXParameter() { }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        public sealed class PFXParameterLeaveState : PFXParameterBase
        {
            // These are the genuine four constant native getters, not placeholders.
            public override bool EvaluateContinuously => false;
            public override bool EndOnLeaveState => false;
            public override bool RemoveEmittedParticles => false;
            public override float RemoveDelaySeconds => 0f;
            public PFXParameterLeaveState() { }
        }

        [Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
        public sealed class FullscreenEffectParameter : ISerializationCallbackReceiver
        {
            [UnityEngine.HideInInspector]
            public string Name;
            public FullscreenShaderParametersType FullscreenShaderParametersType;
            [UnityEngine.TooltipAttribute("If ticked, effect will continue until stopped by an explicit trigger.")]
            public bool UntilStopped;
            [UnityEngine.MinAttribute(0f)]
            [UnityEngine.TooltipAttribute("Set a specific length of time the effect should run for. Must be set to 0 if Until Stopped is ticked.")]
            public float Duration;
            [UnityEngine.TooltipAttribute("If this shader parameter definition has normalised anim curves, apply them over the given duration.")]
            public bool ApplyCurveOverDuration;
            [UnityEngine.TooltipAttribute("If ticked, effect triggers on exiting the state rather than on entering.")]
            public bool TriggerOnExit;
            [UnityEngine.TooltipAttribute("Delay stopping this effect until this many seconds after a stop is requested. Only valid if UntilStopped is used.")]
            [Hardlight.ShowIfAttribute("UntilStopped", (string)null)]
            public float DelayStopSeconds;

            public void OnBeforeSerialize() => UpdateProperties();
            public void OnAfterDeserialize() => UpdateProperties();

            private void UpdateProperties()
            {
                Name = FullscreenShaderParametersType.GetString();
                if (UntilStopped)
                {
                    Name += " (until stopped)";
                    return;
                }
                Name += string.Format(" ({0}s)", Duration);
                if (TriggerOnExit)
                    Name += " (Trigger on exit)";
            }

            // Original 060019ea is RET; it intentionally does not rebuild the name.
            public void OnValidate(UnityEngine.Object context) { }
            public FullscreenEffectParameter() { }
        }
    }
}
