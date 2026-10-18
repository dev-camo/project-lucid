using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000832; 0x606EC0..0x6077E0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AnimatedTargetIndicator : ViewTarget
    {
        [SerializeField]
        private Animator m_targetAnimator;
        [SerializeField]
        private AnimationParameterWrapper m_visionBlockedParameter;
        [SerializeField]
        private AnimationParameterWrapper m_vulnerableParameter;
        [SerializeField]
        private AnimationParameterWrapper m_targetLockedParameter;
        [SerializeField]
        private AnimationParameterWrapper m_attackTargetParameter;
        [Tooltip("How many frames to wait between each raycast to check visibility? Higher number = more performant, less reactive.")]
        [SerializeField]
        private int m_framesBetweenRaycast = 15;
        [SerializeField]
        private LayerMask m_raycastLayerMask;
        private int m_raycastFrameCount;
        private readonly RaycastHit m_emptyRaycastHit;
        private GameObject m_originTarget;
        private bool m_gameplayActive = true;

        // 0x606EC0: the base setup (an inlined ViewTarget.OnSetup in the original) runs
        // first, then the origin target is read and the gameplay-control flag followed.
        protected override void OnSetup(ViewParameters viewParams)
        {
            base.OnSetup(viewParams);
            m_originTarget = ((AnimatedTargetIndicatorParameters)viewParams).OriginTarget;
            ProcessManager.GetSystem<App>(null, true).OnGameplayControlActive += OnOnGameplayControlActive;
        }

        private void OnOnGameplayControlActive(bool isActive)
        {
            m_gameplayActive = isActive;
        }

        private void OnEnable()
        {
            m_targetAnimator.Rebind();
            m_targetAnimator.Update(0f);
        }

        // 0x607150: a valid message for this indicator's origin target and a listed homing
        // type locks on, or fires the attack trigger while active; anything else unlocks.
        public override void UpdateFromTargetMessage(HomingTargetUpdateMessage homingTargetUpdateMessage)
        {
            if (homingTargetUpdateMessage.IsValid && m_originTarget == homingTargetUpdateMessage.TargetValue.Object.Target
                && m_showForHomingTargetTypes.Contains(homingTargetUpdateMessage.TargetValue.Object.Type))
            {
                if (homingTargetUpdateMessage.IsActive)
                {
                    m_attackTargetParameter.TryGetAnimationHash(out int attackHash);
                    m_targetAnimator.SetTrigger(attackHash);
                }
                else
                {
                    m_targetLockedParameter.TryGetAnimationHash(out int lockedHash);
                    m_targetAnimator.SetBool(lockedHash, true);
                }
            }
            else
            {
                m_targetLockedParameter.TryGetAnimationHash(out int lockedHash);
                m_targetAnimator.SetBool(lockedHash, false);
            }
        }

        public void SetVulnerable()
        {
            m_vulnerableParameter.TryGetAnimationHash(out int hash);
            m_targetAnimator.SetTrigger(hash);
        }

        public void SetTargetLocked(bool lockedOn)
        {
            m_targetLockedParameter.TryGetAnimationHash(out int hash);
            m_targetAnimator.SetBool(hash, lockedOn);
        }

        public void SetAttack()
        {
            m_attackTargetParameter.TryGetAnimationHash(out int hash);
            m_targetAnimator.SetTrigger(hash);
        }

        public void SetHiddenBehindObject(bool hidden)
        {
            m_visionBlockedParameter.TryGetAnimationHash(out int hash);
            m_targetAnimator.SetBool(hash, hidden);
        }

        // 0x6073B0: the animator object is shown only while gameplay controls are active.
        protected override void ShowTarget(bool isActive)
        {
            m_targetAnimator.gameObject.SetActive(isActive && m_gameplayActive);
        }

        protected override Vector3 TargetPosition
        {
            get => m_targetAnimator.transform.position;
            set => m_targetAnimator.transform.position = value;
        }

        protected override Vector3 TargetScale
        {
            get => m_targetAnimator.transform.localScale;
            set => m_targetAnimator.transform.localScale = value;
        }

        protected override Color TargetColour { get; set; }

        protected override bool ProjectToScreenSpace => true;

        // 0x607500: the raycast only runs every m_framesBetweenRaycast calls; in between
        // (and while the camera manager is missing) the empty hit is returned as visible.
        public bool CheckIfVisible(out RaycastHit hit, Vector3 targetPosition)
        {
            m_raycastFrameCount++;
            if (m_raycastFrameCount != m_framesBetweenRaycast || m_cameraManagerRef.IsNull())
            {
                hit = m_emptyRaycastHit;
                return true;
            }
            m_raycastFrameCount = 0;
            Camera mainCamera = m_cameraManagerRef.Get().MainCamera;
            float distance = (mainCamera.transform.position - targetPosition).magnitude;
            Ray ray = mainCamera.ScreenPointToRay(mainCamera.WorldToScreenPoint(m_targetWorldPosition.Value + m_worldOffset));
            return Physics.Raycast(ray, out hit, distance, m_raycastLayerMask);
        }

        private void OnDestroy()
        {
            ProcessManager.GetSystem<App>(null, true).OnGameplayControlActive -= OnOnGameplayControlActive;
        }
    }
}
