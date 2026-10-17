// Original HLUnityUI.Runtime Hardlight.HLAlphaInOut 020000a3 and its iterator <RunAnimation>d__4
// 020000a4, from the x86-64 bodies (0x060004d2..0x060004de, 0x1B61300..0x1B61A2A) and metadata.
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class HLAlphaInOut : HLUIAnimationInOut
    {
        [SerializeField]
        private Graphic m_targetGraphic;
        [SerializeField]
        private CanvasGroup m_targetCanvasGroup;
        [SerializeField]
        private HLCurve m_alphaCurve;

        protected override void OnEnable()
        {
            switch (m_initialState)
            {
                case AnimState.In:
                    m_playDirection = PlayDirection.In;
                    ForceFinishAnimation();
                    break;
                case AnimState.Out:
                    m_playDirection = PlayDirection.Out;
                    ForceFinishAnimation();
                    break;
            }
            base.OnEnable();
        }

        // MoveNext 0x060004db: reversing a running animation (AnimatingIn -> out, AnimatingOut -> in)
        // skips the delay wait. The timer resumes from the current progress, and the curve is
        // sampled at the in-time (endTime - timer when animating out).
        protected override IEnumerator RunAnimation()
        {
            bool animatingIn = m_playDirection == PlayDirection.In;
            bool reversing = animatingIn ? CurrentState == AnimState.AnimatingOut : CurrentState == AnimState.AnimatingIn;
            float timer;
            float endTime = m_alphaCurve.EndTime();
            if (animatingIn)
            {
                CurrentState = AnimState.AnimatingIn;
                m_onAnimateIn.Invoke();
                timer = endTime * m_normalisedInOutProgress;
            }
            else
            {
                CurrentState = AnimState.AnimatingOut;
                m_onAnimateOut.Invoke();
                timer = (One - m_normalisedInOutProgress) * endTime;
            }

            if (!reversing)
                yield return animatingIn ? m_animateInDelayWait : m_animateOutDelayWait;

            do
            {
                timer += IndependentTimeDelta();
                float time = animatingIn ? timer : endTime - timer;
                m_normalisedInOutProgress = time / endTime;
                float alpha = m_alphaCurve.SampleAtTime(time);
                UIUtils.SetAlpha(m_targetCanvasGroup, alpha);
                UIUtils.SetAlpha(m_targetGraphic, alpha);
                yield return null;
            }
            while (timer < endTime);

            SetFinalState(animatingIn);
            m_trackedAnimation = null;
        }

        public override void AnimateIn()
        {
            if (CurrentState == AnimState.In || CurrentState == AnimState.AnimatingIn)
                return;

            m_playDirection = PlayDirection.In;
            Animate();
        }

        public override void AnimateOut()
        {
            if (CurrentState == AnimState.Out || CurrentState == AnimState.AnimatingOut)
                return;

            m_playDirection = PlayDirection.Out;
            Animate();
        }

        // Snaps to the curve's end (in) or its time zero (out); with no direction only the stop runs.
        public override void ForceFinishAnimation()
        {
            this.SafeStopCoroutine(ref m_trackedAnimation);
            if (m_playDirection != PlayDirection.None)
            {
                bool animatingIn = m_playDirection == PlayDirection.In;
                float alpha = m_alphaCurve.SampleAtTime(animatingIn ? m_alphaCurve.EndTime() : Zero);
                UIUtils.SetAlpha(m_targetCanvasGroup, alpha);
                UIUtils.SetAlpha(m_targetGraphic, alpha);
                SetFinalState(animatingIn);
                m_playDirection = PlayDirection.None;
            }
        }

        protected override void SetFinalState(bool animatingIn)
        {
            if (animatingIn)
            {
                m_normalisedInOutProgress = One;
                CurrentState = AnimState.In;
                m_onAnimateInComplete.Invoke();
                ScheduleAutoAnimateOut();
            }
            else
            {
                m_normalisedInOutProgress = Zero;
                CurrentState = AnimState.Out;
                m_onAnimateOutComplete.Invoke();
            }
        }
    }
}
