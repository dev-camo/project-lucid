using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleEffectWrapper : TimeScaledParticleSystem
    {
        [SerializeField] private bool m_playOnAwake;
        [SerializeField] private bool m_shouldDisableGameObjectWhenInactive = true;
        [SerializeField] private ParticleSystem[] m_alwaysClearParticlesWhenStopped;
        public readonly Bindable<float> ActorImpactSpeed = new Bindable<float>();
        public readonly Bindable<float> ActorXZVelocity = new Bindable<float>();
        public readonly Bindable<float> BrainMovement = new Bindable<float>();
        public readonly Bindable<float> SpinDashCharge = new Bindable<float>();
        private Action<ParticleEffectWrapper> m_onEffectComplete;
        public ParticleEffectWrapperStatus Status { get; private set; } // 060004ab/ac.
        public ParticleEffectDefinition ParticleEffectDefinition { get; private set; } // ad/ae.
        public ParticleBindingParameters BindingParameters { get; private set; } // af/b0.
        public bool AcquireOnComplete { get; private set; } // b1/b2.
        private Transform m_matchTransformTarget;
        private Vector3 m_matchTransformTargetOffset;
        private Transform m_cachedTransform;
        private bool m_visible;

        protected override void Awake() // 060004b3.
        {
            base.Awake();
            m_cachedTransform = transform;
            _ = m_mainParticleSystem.main.stopAction;
            if (m_playOnAwake)
            {
                Status = ParticleEffectWrapperStatus.Stopped;
                StartEffect();
            }
            else
            {
                StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear);
                Status = ParticleEffectWrapperStatus.Stopped;
                if (m_shouldDisableGameObjectWhenInactive) gameObject.SetActive(false);
                m_visible = false;
            }
        }
        public void SetUp(Action<ParticleEffectWrapper> onEffectComplete, ParticleEffectDefinition particleEffectDefinition) // 060004b4.
        {
            m_onEffectComplete = onEffectComplete;
            ParticleEffectDefinition = particleEffectDefinition;
            AcquireOnComplete = false;
        }
        public void UpdateParameters(ParticleBindingParameters parameters) // 060004b5.
        {
            BindingParameters = parameters;
            ActorImpactSpeed.Value = parameters.ActorImpactVelocity;
            ActorXZVelocity.Value = parameters.ActorXZVelocity;
            BrainMovement.Value = parameters.BrainMovement;
            SpinDashCharge.Value = parameters.SpinDashCharge;
            if (SyncVisibilityWithActor() && m_visible != parameters.ActorVisibility)
            {
                SetRenderers(!m_visible);
                m_visible = parameters.ActorVisibility;
            }
        }
        protected override void InternalUpdate(float deltaTime) // 060004b6.
        {
            base.InternalUpdate(deltaTime);
            if (m_matchTransformTarget != null)
            {
                Quaternion rotation = m_matchTransformTarget.rotation;
                Vector3 rotatedOffset = rotation * m_matchTransformTargetOffset;
                Vector3 position = m_matchTransformTarget.position;
                m_cachedTransform.SetPositionAndRotation(rotatedOffset + position, rotation);
            }
        }
        private void OnParticleSystemStopped() // 060004b7.
        {
            if (m_shouldDisableGameObjectWhenInactive) gameObject.SetActive(false);
            m_visible = false;
            Status = ParticleEffectWrapperStatus.Stopped;
            m_matchTransformTarget = null;
            Action<ParticleEffectWrapper> callback = m_onEffectComplete;
            m_onEffectComplete = null;
            callback?.Invoke(this);
        }
        public void StartEffect() // 060004b8.
        {
            gameObject.SetActive(true);
            if (SyncVisibilityWithActor()) SetRenderers(true);
            m_visible = true;
            Status = ParticleEffectWrapperStatus.Started;
            m_mainParticleSystem.Play(true);
        }
        public void StopEffect(ParticleSystemStopBehavior behaviour, bool acquireOnComplete = false) // 060004b9.
        {
            Status = ParticleEffectWrapperStatus.Stopping;
            m_mainParticleSystem.Stop(true, behaviour);
            AcquireOnComplete = acquireOnComplete;
            foreach (TrailRenderer renderer in m_trailRenderers) renderer.Clear();
            if (behaviour != ParticleSystemStopBehavior.StopEmittingAndClear && m_alwaysClearParticlesWhenStopped != null)
                foreach (ParticleSystem system in m_alwaysClearParticlesWhenStopped)
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void StopEffectEmittingAndClear() // 060004ba.
        {
            StopEffect(ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void SetPositionAndRotation(Transform target, Vector3 followOffset, bool keepFollowingTransform) // 060004bb.
        {
            Quaternion rotation = target.rotation;
            Vector3 rotatedOffset = rotation * followOffset;
            Vector3 position = target.position;
            m_cachedTransform.SetPositionAndRotation(rotatedOffset + position, rotation);
            m_matchTransformTarget = keepFollowingTransform ? target : null;
            m_matchTransformTargetOffset = keepFollowingTransform ? followOffset : Vector3.zero;
        }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) // 060004bc.
        {
            m_cachedTransform.SetPositionAndRotation(position, rotation);
        }
        protected override TimeCategory GetDefaultTimeCategoryEnum() => TimeCategory.Effects; // 060004bd.
        protected override UpdateOn GetDefaultUpdateOn() => UpdateOn.Update; // 060004be.
        private bool SyncVisibilityWithActor() // 060004bf: original CLR type test.
        {
            return ParticleEffectDefinition is ActorParticleEffectDefinition definition && definition.SyncVisibilityWithActor;
        }
        public ParticleEffectWrapper() { } // 060004c0; four bindables initialize before genuine base.

        public enum ParticleEffectWrapperStatus { Stopped = 0, Started = 1, Stopping = 2 }
        public struct ParticleBindingParameters // Original field-only value type; zero MethodDefs.
        {
            public float ActorImpactVelocity;
            public float ActorXZVelocity;
            public bool ActorVisibility;
            public float BrainMovement;
            public float SpinDashCharge;
        }
    }
}
