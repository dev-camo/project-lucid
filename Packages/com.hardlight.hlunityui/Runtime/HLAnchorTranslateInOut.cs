// Original HLUnityUI.Runtime Hardlight.HLAnchorTranslateInOut 020000a5 with TranslationDirection
// 020000a6, MotionType 020000a7 and <RunAnimation>d__15 020000a8, from the x86-64 bodies
// (0x060004df..0x060004ee, 0x1B61A30..0x1B624CA) and metadata.
using System.Collections;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(RectTransform))]
    public class HLAnchorTranslateInOut : HLUIAnimationInOut
    {
        public enum TranslationDirection
        {
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop
        }

        public enum MotionType
        {
            Simple,
            Bounce,
            Stretch
        }

        [Tooltip("If this transform is null, it will select itself at runtime")]
        [SerializeField]
        private RectTransform m_targetTransform;
        [SerializeField]
        [HashEnum(typeof(MotionType))]
        private MotionType motionType;
        [SerializeField]
        [HashEnum(typeof(TranslationDirection))]
        private TranslationDirection translationOnDirection;
        [SerializeField]
        private float m_clampedAnchorOut = -1f;
        [SerializeField]
        private float m_clampedAnchorIn;
        [SerializeField]
        private float m_stretchAnchorOut;
        [SerializeField]
        private float m_stretchAnchorIn = 1f;
        [SerializeField]
        private HLCurve m_translationCurve;

        protected override void Awake()
        {
            base.Awake();
            if (m_targetTransform == null)
                m_targetTransform = transform as RectTransform;
        }

        protected override void OnEnable()
        {
            switch (m_initialState)
            {
                case AnimState.In:
                    m_normalisedInOutProgress = One;
                    SetAnchors(m_clampedAnchorIn, m_stretchAnchorIn);
                    break;
                case AnimState.Out:
                    m_normalisedInOutProgress = Zero;
                    SetAnchors(m_clampedAnchorOut, m_stretchAnchorOut);
                    break;
            }
            base.OnEnable();
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

        // m_playDirection is left as it is; a later force finish repeats the last direction.
        public override void ForceFinishAnimation()
        {
            this.SafeStopCoroutine(ref m_trackedAnimation);
            SetFinalState(m_playDirection == PlayDirection.In);
        }

        // MoveNext 0x060004eb: as HLAlphaInOut, a reversal skips the delay and the timer resumes from
        // the current progress. Simple clamps both lerps, Bounce clamps neither, and Stretch clamps
        // only the clamped edge; any other motion type drives both edges to zero.
        protected override IEnumerator RunAnimation()
        {
            bool animatingIn = m_playDirection == PlayDirection.In;
            bool reversing = animatingIn ? CurrentState == AnimState.AnimatingOut : CurrentState == AnimState.AnimatingIn;
            float timer;
            float endTime = m_translationCurve.EndTime();
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
                float progress = m_translationCurve.SampleAtTime(time);
                float clamped = Zero;
                float stretched = Zero;
                switch (motionType)
                {
                    case MotionType.Simple:
                        clamped = Mathf.Lerp(m_clampedAnchorOut, m_clampedAnchorIn, progress);
                        stretched = Mathf.Lerp(m_stretchAnchorOut, m_stretchAnchorIn, progress);
                        break;
                    case MotionType.Bounce:
                        clamped = Mathf.LerpUnclamped(m_clampedAnchorOut, m_clampedAnchorIn, progress);
                        stretched = Mathf.LerpUnclamped(m_stretchAnchorOut, m_stretchAnchorIn, progress);
                        break;
                    case MotionType.Stretch:
                        clamped = Mathf.Lerp(m_clampedAnchorOut, m_clampedAnchorIn, progress);
                        stretched = Mathf.LerpUnclamped(m_stretchAnchorOut, m_stretchAnchorIn, progress);
                        break;
                }
                SetAnchors(clamped, stretched);
                yield return null;
            }
            while (timer < endTime);

            SetFinalState(animatingIn);
            m_trackedAnimation = null;
        }

        // Despite the name, the pairs come from the current anchors: the in pair is the current
        // anchors on the direction's axis and the out pair is (0 - span, 0) or (span + 1, 1).
        public void SetAnchorTargetsFromConstants()
        {
            Vector2 anchorMin = m_targetTransform.anchorMin;
            Vector2 anchorMax = m_targetTransform.anchorMax;
            float width = anchorMax.x - anchorMin.x;
            float height = anchorMax.y - anchorMin.y;
            switch (translationOnDirection)
            {
                case TranslationDirection.LeftToRight:
                    m_clampedAnchorOut = Zero - width;
                    m_clampedAnchorIn = anchorMin.x;
                    m_stretchAnchorOut = Zero;
                    m_stretchAnchorIn = anchorMax.x;
                    break;
                case TranslationDirection.RightToLeft:
                    m_clampedAnchorOut = width + One;
                    m_clampedAnchorIn = anchorMax.x;
                    m_stretchAnchorOut = One;
                    m_stretchAnchorIn = anchorMin.x;
                    break;
                case TranslationDirection.TopToBottom:
                    m_clampedAnchorOut = height + One;
                    m_clampedAnchorIn = anchorMax.y;
                    m_stretchAnchorOut = One;
                    m_stretchAnchorIn = anchorMin.y;
                    break;
                case TranslationDirection.BottomToTop:
                    m_clampedAnchorOut = Zero - height;
                    m_clampedAnchorIn = anchorMin.y;
                    m_stretchAnchorOut = Zero;
                    m_stretchAnchorIn = anchorMax.y;
                    break;
            }
        }

        // LeftToRight and BottomToTop write clamped to anchorMin and stretched to anchorMax;
        // RightToLeft and TopToBottom swap them; values outside 0..3 change nothing.
        public void SetAnchors(float clamped, float stretched)
        {
            switch (translationOnDirection)
            {
                case TranslationDirection.LeftToRight:
                    m_targetTransform.anchorMin = new Vector2(clamped, m_targetTransform.anchorMin.y);
                    m_targetTransform.anchorMax = new Vector2(stretched, m_targetTransform.anchorMax.y);
                    break;
                case TranslationDirection.RightToLeft:
                    m_targetTransform.anchorMin = new Vector2(stretched, m_targetTransform.anchorMin.y);
                    m_targetTransform.anchorMax = new Vector2(clamped, m_targetTransform.anchorMax.y);
                    break;
                case TranslationDirection.TopToBottom:
                    m_targetTransform.anchorMin = new Vector2(m_targetTransform.anchorMin.x, stretched);
                    m_targetTransform.anchorMax = new Vector2(m_targetTransform.anchorMax.x, clamped);
                    break;
                case TranslationDirection.BottomToTop:
                    m_targetTransform.anchorMin = new Vector2(m_targetTransform.anchorMin.x, clamped);
                    m_targetTransform.anchorMax = new Vector2(m_targetTransform.anchorMax.x, stretched);
                    break;
            }
        }

        protected override void SetFinalState(bool animatingIn)
        {
            if (animatingIn)
            {
                CurrentState = AnimState.In;
                SetAnchors(m_clampedAnchorIn, m_stretchAnchorIn);
                m_onAnimateInComplete.Invoke();
                m_normalisedInOutProgress = One;
                ScheduleAutoAnimateOut();
            }
            else
            {
                CurrentState = AnimState.Out;
                SetAnchors(m_clampedAnchorOut, m_stretchAnchorOut);
                m_onAnimateOutComplete.Invoke();
                m_normalisedInOutProgress = Zero;
            }
        }
    }
}
