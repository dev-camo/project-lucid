// Original HLUnityUI.Runtime 02000020 (TypeDefIndex 11858) with State and the StartTransition closure;
// bodies translated from the 1.10.1 x86-64 binary (0x060000e0..ef).
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class FullScreenTransitionController : MonoBehaviour
    {
        public enum State
        {
            HeldOut,
            TransitioningIn,
            HeldIn,
            TransitioningOut
        }

        [SerializeField]
        private FullScreenTransitionType m_type;
        [SerializeField]
        private Animator m_animator;
        [SerializeField]
        private string m_animateInTrigger;
        [SerializeField]
        private string m_animateOutTrigger;
        [SerializeField]
        private string m_animatorResetTrigger;
        [SerializeField]
        private float m_transitionInDuration = 1f;
        [SerializeField]
        private float m_transitionOutDuration = 1f;
        [SerializeField]
        private UnityEvent m_onTransitionIn;
        [SerializeField]
        private UnityEvent m_onTransitionInComplete;
        [SerializeField]
        private UnityEvent m_onTransitionOut;
        [SerializeField]
        private UnityEvent m_onTransitionOutComplete;
        [SerializeField]
        private UnityEvent m_onReset;
        private float m_overriddenTransitionInDuration;
        private float m_overriddenTransitionOutDuration;
        private Action m_cachedMidpointCallback;
        private Action m_cachedCompletionCallback;

        public FullScreenTransitionType Type
        {
            get { return m_type; }
            private set { m_type = value; }
        }

        public State CurrentState { get; private set; }

        public void Awake()
        {
            CurrentState = State.HeldOut;
        }

        // 0x060000e5: the closure (<>c__DisplayClass25_0) is created on entry, before the guard.
        // Continuous: override * 0.5f is the stored override for both legs, and each leg still falls back to its
        // serialized duration unless that half is above zero. The cached midpoint runs the caller's callback, then
        // ContinueTransition(half), which only continues if the state is still HeldIn (b__0 0x060000ef).
        public bool StartTransition(float overrideTransitionDuration = 0f, Action midPointCallback = null, Action completionCallback = null)
        {
            if (CurrentState != State.HeldOut)
                return false;

            // Method scope keeps one display class with the original field set and order (dump.cs 11857).
            float halfTotalDurationOverride;
            if (m_type != FullScreenTransitionType.Continuous)
            {
                m_overriddenTransitionInDuration = overrideTransitionDuration;
                m_cachedMidpointCallback = midPointCallback;
            }
            else
            {
                halfTotalDurationOverride = overrideTransitionDuration * 0.5f;
                m_overriddenTransitionInDuration = halfTotalDurationOverride;
                m_cachedMidpointCallback = () =>
                {
                    midPointCallback?.Invoke();
                    ContinueTransition(halfTotalDurationOverride);
                };
            }

            m_cachedCompletionCallback = completionCallback;
            TransitionIn();
            return true;
        }

        // 0x060000e6: a completion passed here is only adopted when StartTransition cached none.
        public bool ContinueTransition(float overrideTransitionDuration = 0f, Action completionCallback = null)
        {
            if (CurrentState != State.HeldIn)
                return false;

            m_overriddenTransitionOutDuration = overrideTransitionDuration;
            if (completionCallback != null && m_cachedCompletionCallback == null)
                m_cachedCompletionCallback = completionCallback;

            TransitionOut();
            return true;
        }

        public void ResetTransition()
        {
            CurrentState = State.HeldOut;
            m_animator.SetTrigger(m_animatorResetTrigger);
            m_onReset.Invoke();
        }

        // 0x060000e8: TransitioningIn reaches the midpoint, TransitioningOut completes; HeldOut, HeldIn or any other
        // value is a no-op. Its caller is not in the evidence (no shipped asset names it).
        public void Action_TransitionAnimationComplete()
        {
            switch (CurrentState)
            {
                case State.TransitioningIn:
                    OnMidPointReached();
                    break;
                case State.TransitioningOut:
                    OnCompletion();
                    break;
            }
        }

        // Overrides only apply when above zero (0x060000e9/ea ucomiss + cmova); NaN, zero and negatives fall back.
        private void TransitionIn()
        {
            CurrentState = State.TransitioningIn;
            m_animator.speed = 1f / (m_overriddenTransitionInDuration > 0f ? m_overriddenTransitionInDuration : m_transitionInDuration);
            m_animator.SetTrigger(m_animateInTrigger);
            m_onTransitionIn.Invoke();
        }

        private void TransitionOut()
        {
            CurrentState = State.TransitioningOut;
            m_animator.speed = 1f / (m_overriddenTransitionOutDuration > 0f ? m_overriddenTransitionOutDuration : m_transitionOutDuration);
            m_animator.SetTrigger(m_animateOutTrigger);
            m_onTransitionOut.Invoke();
        }

        private void OnMidPointReached()
        {
            CurrentState = State.HeldIn;
            m_onTransitionInComplete.Invoke();
            m_cachedMidpointCallback?.Invoke();
        }

        private void OnCompletion()
        {
            CurrentState = State.HeldOut;
            m_onTransitionOutComplete.Invoke();
            m_cachedCompletionCallback?.Invoke();
        }
    }
}
