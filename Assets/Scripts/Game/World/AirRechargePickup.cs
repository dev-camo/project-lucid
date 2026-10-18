using Hardlight;
using Hardlight.UI.Binding;
using System;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000ab9; 0x6E1100..0x6E2040 (constructor 0x6E2040). The type carries
    // no Il2CppSetOption attributes, so every dereference keeps its null check.
    public class AirRechargePickup : TimeScaledComponent_SDT
    {
        [SerializeField]
        private CharacterArchetype m_characterArchetype = CharacterArchetype.Flying;
        [SerializeField]
        private Animation m_animation;
        [SerializeField]
        private AnimationClip m_animationIdle;
        [SerializeField]
        private AnimationClip m_animationCollect;
        [SerializeField]
        private AnimationClip m_animationCooldown;
        [SerializeField]
        private ParticleSystem m_pfxIdle;
        [SerializeField]
        private ParticleEffectSpawner m_pfxSpawnerCollect;
        [SerializeField]
        private ParticleEffectSpawner m_pfxSpawnerRecharged;
        [Tooltip("How long to wait after character has left collision to re-enter the idle visual state and play the recharged PFX")]
        [SerializeField]
        [Min(0)]
        private float m_rechargeTimeSeconds;
        [SerializeField]
        private bool m_useCustomRenderBounds;
        [ShowIf("m_useCustomRenderBounds")]
        [SerializeField]
        private float m_cullingBoundsSize = 2f;
        [Tooltip("Auto-populated when subsystem list regenerated, if using custom render bounds.")]
        [SerializeField]
        [ShowIf("m_useCustomRenderBounds")]
        private Renderer[] m_particleRenderers;
        public readonly Bindable<bool> IsValid = new Bindable<bool>();
        private readonly SystemRef<App> m_appRef = ProcessManager.GetSystemRef<App>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>(null, true);
        private float m_enterIdleTimeRemainingSeconds;
        private float m_enterCooldownAnimationTimeRemainingSeconds;
        private CullingGroup m_cullingGroup;
        private DetectorArchetypeUpdateMessage m_detectorArchetypeMessage;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnterIdleState(false);
            if (m_useCustomRenderBounds)
                m_cinemachineCameraManagerRef.InvokeOnValid(manager => manager.OnMainCameraReady += SetupCustomCulling);
            IsValid.Value = true;
            m_messageManagerRef.Get().ComponentMessagesWithCompletion
                .SubscribeToMessage(m_appRef.Get(), new MessageCallbackWithCompletion<DetectorArchetypeUpdateMessage>(OnDetectorActive));
        }

        // The camera-manager and message unsubscriptions are guarded by SystemRef.IsValid; the culling
        // group is released behind a plain null test.
        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_cinemachineCameraManagerRef.IsValid())
                m_cinemachineCameraManagerRef.Get().OnMainCameraReady -= SetupCustomCulling;
            if (m_messageManagerRef.IsValid() && m_appRef.IsValid())
                m_messageManagerRef.Get().ComponentMessagesWithCompletion
                    .UnsubscribeFromMessage(m_appRef.Get(), new MessageCallbackWithCompletion<DetectorArchetypeUpdateMessage>(OnDetectorActive));
            if (m_cullingGroup != null)
            {
                m_cullingGroup.onStateChanged -= OnStateChanged;
                m_cullingGroup.Dispose();
                m_cullingGroup = null;
            }
        }

        // The completion callback is never invoked. A matching archetype message is
        // remembered and answered once the character is available.
        private void OnDetectorActive(in DetectorArchetypeUpdateMessage data, Action completed)
        {
            if (data.Archetype == m_characterArchetype)
            {
                m_detectorArchetypeMessage = data;
                m_characterManagerRef.Get().RegisterOnCharacterChange(OnDetectorCharacterActive, false);
            }
        }

        private void OnDetectorCharacterActive(Character character)
        {
            m_characterManagerRef.Get().ReleaseOnCharacterChange(OnDetectorCharacterActive);
            IsValid.Value = m_detectorArchetypeMessage.IsValid;
        }

        protected override void InternalUpdate(float deltaTime)
        {
            if (m_enterIdleTimeRemainingSeconds > 0f)
            {
                m_enterIdleTimeRemainingSeconds -= deltaTime;
                if (m_enterIdleTimeRemainingSeconds <= 0f)
                    EnterIdleState(true);
            }
            if (m_enterCooldownAnimationTimeRemainingSeconds > 0f)
            {
                m_enterCooldownAnimationTimeRemainingSeconds -= deltaTime;
                if (m_enterCooldownAnimationTimeRemainingSeconds <= 0f)
                    SetAnimation(m_animationCooldown, false);
            }
        }

        private void SetAnimation(AnimationClip clip, bool loop)
        {
            m_animation.Stop();
            m_animation.clip = clip;
            m_animation.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            m_animation.Play();
        }

        public void Action_CharacterEnter(Collider characterCollider)
        {
            RechargeAbilities(characterCollider, false);
            EnterCollectState();
        }

        public void Action_CharacterStay(Collider characterCollider)
        {
            RechargeAbilities(characterCollider, false);
        }

        // Leaving starts the countdown to the idle visual state.
        public void Action_CharacterExit(Collider characterCollider)
        {
            RechargeAbilities(characterCollider, true);
            m_enterIdleTimeRemainingSeconds = m_rechargeTimeSeconds;
        }

        private void EnterIdleState(bool playRechargedPfx)
        {
            m_enterIdleTimeRemainingSeconds = 0f;
            m_enterCooldownAnimationTimeRemainingSeconds = 0f;
            SetAnimation(m_animationIdle, true);
            m_pfxIdle.gameObject.SetActive(true);
            m_pfxIdle.Play(true);
            if (playRechargedPfx)
                m_pfxSpawnerRecharged.Spawn();
        }

        private void EnterCollectState()
        {
            m_enterIdleTimeRemainingSeconds = 0f;
            SetAnimation(m_animationCollect, false);
            m_pfxIdle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            m_pfxSpawnerCollect.Spawn();
            m_enterCooldownAnimationTimeRemainingSeconds = m_animationCollect.length;
        }

        private void RechargeAbilities(Collider characterCollider, bool isExit)
        {
            GetCharacter(characterCollider).GetAbilitiesOfType<CharacterAbility_Air>(ability => ability.Recharge(isExit));
        }

        // The collider is ignored: the current character of the manager is always used.
        private Character GetCharacter(Collider characterCollider)
        {
            return m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
        }

        protected override TimeCategory GetDefaultTimeCategoryEnum()
        {
            return TimeCategory.Environment;
        }

        protected override UpdateOn GetDefaultUpdateOn()
        {
            return UpdateOn.Update;
        }

        // The culling group tracks one sphere at this pickup; an existing group only
        // gets the new camera.
        private void SetupCustomCulling(Camera mainCamera)
        {
            if (this == null)
                return;
            if (m_cullingGroup == null)
            {
                m_cullingGroup = new CullingGroup();
                m_cullingGroup.SetBoundingSpheres(new[] { new BoundingSphere(transform.position, m_cullingBoundsSize) });
                m_cullingGroup.SetBoundingSphereCount(1);
                m_cullingGroup.onStateChanged += OnStateChanged;
                Cull(m_cullingGroup.IsVisible(0));
            }
            m_cullingGroup.targetCamera = mainCamera;
            m_cullingGroup.enabled = true;
        }

        private void OnStateChanged(CullingGroupEvent sphere)
        {
            Cull(sphere.isVisible);
        }

        private void Cull(bool visible)
        {
            if (m_pfxIdle == null)
                return;
            if (visible)
            {
                m_pfxIdle.Play(true);
                SetRenderers(true);
            }
            else
            {
                m_pfxIdle.Pause(true);
                SetRenderers(false);
            }
        }

        private void SetRenderers(bool enable)
        {
            for (int i = 0; i < m_particleRenderers.Length; i++)
                m_particleRenderers[i].enabled = enable;
        }

        // Editor-time auto-population of the renderer list.
        protected override void OnValidate()
        {
            base.OnValidate();
            if (m_useCustomRenderBounds && m_particleRenderers.Length == 0)
                m_particleRenderers = m_pfxIdle.GetComponentsInChildren<Renderer>();
        }
    }
}
