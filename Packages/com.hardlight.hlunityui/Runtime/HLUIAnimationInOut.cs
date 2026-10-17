// Original HLUnityUI.Runtime Hardlight.HLUIAnimationInOut 020000ab with PlayDirection 020000ac,
// AnimState 020000ad and <AutoAnimateOut>d__36 020000ae, from the x86-64 bodies
// (0x060004fe..0x06000512, 0x1B616D0..0x1B6301A) and metadata. The nested enums, the constants
// and the CurrentState property precede the fields as declared, which makes AutoAnimateOut
// member ordinal 36.
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class HLUIAnimationInOut : MonoBehaviour
    {
        public enum PlayDirection
        {
            None,
            In,
            Out
        }

        public enum AnimState
        {
            None,
            In,
            AnimatingIn,
            Out,
            AnimatingOut
        }

        protected const float Zero = 0f;
        protected const float One = 1f;

        public AnimState CurrentState { get; protected set; }

        [SerializeField]
        protected AnimState m_initialState;
        [SerializeField]
        protected AnimState m_animateToStateOnEnable;
        [SerializeField]
        protected bool m_useUnscaledTime;
        [SerializeField]
        protected bool m_forceActivateGameObjectBeforeAnimation;
        // Neither m_shouldDelay flag is read by this type or by HLAlphaInOut, HLScaleInOut or
        // HLAnchorTranslateInOut: their RunAnimation bodies yield the Awake-built waits regardless.
        [SerializeField]
        protected bool m_shouldDelayAnimateIn;
        [SerializeField]
        protected float m_animateInDelaySeconds;
        [SerializeField]
        protected bool m_shouldDelayAnimateOut;
        [SerializeField]
        protected float m_animateOutDelaySeconds;
        [SerializeField]
        protected bool m_autoAnimateOutAfterTimeout;
        [SerializeField]
        protected float m_autoAnimateOutTimeoutSeconds;
        [SerializeField]
        protected UnityEvent m_onAnimateIn;
        [SerializeField]
        protected UnityEvent m_onAnimateInStart;
        [SerializeField]
        protected UnityEvent m_onAnimateInComplete;
        [SerializeField]
        protected UnityEvent m_onAnimateOut;
        [SerializeField]
        protected UnityEvent m_onAnimateOutStart;
        [SerializeField]
        protected UnityEvent m_onAnimateOutComplete;
        protected PlayDirection m_playDirection;
        protected Coroutine m_trackedAnimation;
        protected Coroutine m_trackedAutoAnimateOut;
        protected WaitForSeconds m_autoAnimateOutWait;
        protected WaitForSeconds m_animateInDelayWait;
        protected WaitForSeconds m_animateOutDelayWait;
        protected float m_normalisedInOutProgress;

        // The waits are only built here; later changes to the serialized seconds need another Awake.
        protected virtual void Awake()
        {
            m_autoAnimateOutWait = new WaitForSeconds(m_autoAnimateOutTimeoutSeconds);
            m_animateInDelayWait = new WaitForSeconds(m_animateInDelaySeconds);
            m_animateOutDelayWait = new WaitForSeconds(m_animateOutDelaySeconds);
        }

        protected virtual void OnEnable()
        {
            CurrentState = m_initialState;
            if (m_animateToStateOnEnable == m_initialState)
                return;

            switch (m_animateToStateOnEnable)
            {
                case AnimState.In:
                    AnimateIn();
                    break;
                case AnimState.Out:
                    AnimateOut();
                    break;
            }
        }

        protected virtual void OnDisable()
        {
            this.SafeStopCoroutine(ref m_trackedAnimation);
            this.SafeStopCoroutine(ref m_trackedAutoAnimateOut);
            ForceFinishAnimation();
        }

        protected void ScheduleAutoAnimateOut()
        {
            if (m_autoAnimateOutAfterTimeout)
                m_trackedAutoAnimateOut = StartCoroutine(AutoAnimateOut());
        }

        protected virtual void Animate()
        {
            if (m_playDirection != PlayDirection.None && gameObject.activeInHierarchy)
            {
                this.SafeStopCoroutine(ref m_trackedAnimation);
                m_trackedAnimation = StartCoroutine(RunAnimation());
            }
        }

        protected virtual IEnumerator AutoAnimateOut()
        {
            yield return m_autoAnimateOutWait;
            AnimateOut();
        }

        protected abstract IEnumerator RunAnimation();

        public abstract void AnimateIn();

        public abstract void AnimateOut();

        public abstract void ForceFinishAnimation();

        protected abstract void SetFinalState(bool animatingIn);

        protected float IndependentTimeDelta()
        {
            return m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
