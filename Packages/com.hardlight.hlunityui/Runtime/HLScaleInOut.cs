// Original HLUnityUI.Runtime Hardlight.HLScaleInOut 020000a9 and its iterator <RunAnimation>d__8
// 020000aa, from the x86-64 bodies (0x060004ef..0x060004fd, 0x1B624D0..0x1B62DAA) and metadata.
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class HLScaleInOut : HLUIAnimationInOut
    {
        [Tooltip("If this transform is null, it will select itself at runtime")]
        [SerializeField]
        private Transform m_targetTransform;
        [SerializeField]
        private HLCurve m_xCurve;
        [SerializeField]
        private HLCurve m_yCurve;

        protected override void Awake()
        {
            base.Awake();
            if (m_targetTransform == null)
                m_targetTransform = transform;
        }

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

        [ContextMenu("ScaleIn")]
        public override void AnimateIn()
        {
            if (CurrentState == AnimState.In || CurrentState == AnimState.AnimatingIn)
                return;

            if (m_forceActivateGameObjectBeforeAnimation && !gameObject.activeSelf)
                gameObject.SetActive(true);

            m_playDirection = PlayDirection.In;
            Animate();
        }

        [ContextMenu("ScaleOut")]
        public override void AnimateOut()
        {
            if (CurrentState == AnimState.Out || CurrentState == AnimState.AnimatingOut)
                return;

            m_playDirection = PlayDirection.Out;
            Animate();
        }

        // Unlike the alpha and anchor variants, nothing (not even the coroutine stop) runs while the
        // object is inactive in the hierarchy. Each axis snaps to its own curve's end time.
        public override void ForceFinishAnimation()
        {
            if (gameObject.activeInHierarchy && m_playDirection != PlayDirection.None)
            {
                bool animatingIn = m_playDirection == PlayDirection.In;
                this.SafeStopCoroutine(ref m_trackedAnimation);
                float xTime = animatingIn ? m_xCurve.EndTime() : Zero;
                float yTime = animatingIn ? m_yCurve.EndTime() : Zero;
                m_targetTransform.localScale = new Vector3(m_xCurve.SampleAtTime(xTime), m_yCurve.SampleAtTime(yTime), One);
                SetFinalState(animatingIn);
                m_playDirection = PlayDirection.None;
            }
        }

        // MoveNext 0x060004fa: every run yields its delay wait and restarts from time zero, whatever
        // the current progress, and both curves share the longer of the two end times.
        protected override IEnumerator RunAnimation()
        {
            bool animatingIn = m_playDirection == PlayDirection.In;
            if (animatingIn)
            {
                CurrentState = AnimState.AnimatingIn;
                m_onAnimateIn.Invoke();
                yield return m_animateInDelayWait;
                m_onAnimateInStart.Invoke();
            }
            else
            {
                CurrentState = AnimState.AnimatingOut;
                m_onAnimateOut.Invoke();
                yield return m_animateOutDelayWait;
                m_onAnimateOutStart.Invoke();
            }

            float timer = Zero;
            float longestEndTime = Mathf.Max(m_xCurve.EndTime(), m_yCurve.EndTime());
            do
            {
                timer += IndependentTimeDelta();
                AnimationIteration(timer, longestEndTime);
                yield return null;
            }
            while (timer < longestEndTime);

            m_trackedAnimation = null;
            SetFinalState(animatingIn);
            m_playDirection = PlayDirection.None;
        }

        private void AnimationIteration(float currentTime, float endTime)
        {
            float time = m_playDirection == PlayDirection.In ? currentTime : endTime - currentTime;
            time = Mathf.Clamp(time, Zero, endTime);
            m_targetTransform.localScale = new Vector3(m_xCurve.SampleAtTime(time), m_yCurve.SampleAtTime(time), One);
        }

        protected override void SetFinalState(bool animatingIn)
        {
            if (animatingIn)
            {
                CurrentState = AnimState.In;
                m_normalisedInOutProgress = One;
                m_onAnimateInComplete.Invoke();
                ScheduleAutoAnimateOut();
            }
            else
            {
                CurrentState = AnimState.Out;
                m_normalisedInOutProgress = Zero;
                m_onAnimateOutComplete.Invoke();
            }
        }
    }
}
