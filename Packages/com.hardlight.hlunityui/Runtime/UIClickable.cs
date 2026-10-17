// Original HLUnityUI.Runtime Hardlight.UIClickable (TypeDef 0x0200006f), from the x86-64 bodies
// 0x1B510E0..0x1B54080 (methods 0x0600034d..0x0600037a). It is the UISelectable twin of
// HLCurveAnimatedGraphic: curve-driven press/release/disabled animations plus a global click cooldown.
// Member order fixes the iterator names <AnimateOnPress>d__34..<GlobalCooldownClicks>d__37
// (18 fields, the nested enum, two properties with three accessors and ten methods precede them);
// the nested enum's exact position before AnimateOnPress is inferred.
// The x/y sample locals keep the observed order: both curves are sampled before the target is read.
// No shipped prefab, scene or asset references the exported script GUID c8f5642dfc008bc66e8d60dd1a2cb300.
using System.Collections;
using System.Collections.Generic;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIClickable : UISelectable, IPointerClickHandler, IEventSystemHandler, ISubmitHandler
    {
        private enum AnimationState
        {
            NotAnimating = 0,
            AnimatingPress = 1,
            AnimatingRelease = 2,
            AnimatingDisabled = 3
        }

        private static bool s_globalInputOnCooldown;
        [SerializeField]
        protected float m_cooldownTimeInSeconds = 0.15f;
        [SerializeField]
        protected bool m_animationDisabled;
        [SerializeField]
        protected bool m_allowMultiTouchClickthrough;
        [SerializeField]
        protected HLCurveAnimatedGraphicCurveCollection m_curveCollection;
        private Vector3 m_cachedLocalPosition;
        private Vector3 m_cachedLocalScale;
        private Vector3 m_cachedLocalRotation;
        private bool m_hasCached;
        protected bool m_clickAccepted;
        private bool m_shouldResetInactiveAnim;
        // Only ever cleared (Awake, OnDisable), so the Pressed transition never sees it set.
        private bool m_onCooldown;
        private bool m_hasStatusEffects;
        private readonly List<ButtonStatusEffects> m_buttonStatusEffects = new List<ButtonStatusEffects>();
        private AnimationState m_currentAnimatingState;
        private Coroutine m_animatingCoroutine;
        private static Coroutine m_cooldownCoroutine;
        private Coroutine m_waitForUICacheLocalTransformCoroutine;

        public float CooldownTimeInSeconds
        {
            get { return m_cooldownTimeInSeconds; }
        }

        public bool AnimationDisabled
        {
            get { return m_animationDisabled; }
            set { m_animationDisabled = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            m_onCooldown = false;
        }

        // The transform is cached two UI frames after enabling, and only in play mode.
        protected override void OnEnable()
        {
            if (Application.isPlaying)
                m_waitForUICacheLocalTransformCoroutine = CoroutineUtils.WaitForUI(CacheLocalTransform);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_onCooldown = false;
            this.SafeStopCoroutine(ref m_animatingCoroutine);
            CoroutineUtils.StopUtilCoroutine(ref m_waitForUICacheLocalTransformCoroutine);
            m_currentAnimatingState = AnimationState.NotAnimating;
            ResetTransformToCache();
        }

        private void CacheLocalTransform()
        {
            m_cachedLocalPosition = TargetTransform.localPosition;
            m_cachedLocalScale = TargetTransform.localScale;
            m_cachedLocalRotation = TargetTransform.localEulerAngles;
            m_hasCached = true;
        }

        private void ResetTransformToCache()
        {
            if (Application.isPlaying && m_hasCached)
            {
                TargetTransform.localPosition = m_cachedLocalPosition;
                TargetTransform.localScale = m_cachedLocalScale;
                TargetTransform.localEulerAngles = m_cachedLocalRotation;
            }
        }

        // Only pointer ids -1 (left mouse) and 0 (first touch) click unless multi-touch clickthrough is allowed.
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (m_allowMultiTouchClickthrough || eventData.pointerId == -1 || eventData.pointerId == 0)
            {
                Press();
                if (IsActive())
                {
                    EvaluateAndTransitionToSelectionState(eventData);
                    m_clickAccepted = false;
                }
            }
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            Press();
            if (IsActive() && IsInteractable())
                EvaluateAndTransitionToSelectionState(eventData);
        }

        // A rejected press plays the disabled shake; pressing during that shake only restarts its timer.
        // The result is the m_clickAccepted field as it stands after any coroutine has started.
        public virtual bool Press()
        {
            if (!IsActive() || !IsInteractable() || s_globalInputOnCooldown)
            {
                m_clickAccepted = false;
                if (m_animatingCoroutine != null)
                {
                    if (m_currentAnimatingState == AnimationState.AnimatingDisabled)
                    {
                        m_shouldResetInactiveAnim = true;
                        return m_clickAccepted;
                    }
                    StopAnimationCoroutine();
                }
                if (!m_animationDisabled)
                {
                    ResetTransformToCache();
                    m_animatingCoroutine = StartCoroutine(AnimateOnPressDisabled());
                }
                return m_clickAccepted;
            }

            m_clickAccepted = m_currentAnimatingState != AnimationState.AnimatingDisabled;
            if (m_cooldownTimeInSeconds > 0f && gameObject.activeInHierarchy && m_clickAccepted)
            {
                CoroutineUtils.StopUtilCoroutine(ref m_cooldownCoroutine);
                m_cooldownCoroutine = CoroutineUtils.RunCoroutine(GlobalCooldownClicks());
            }
            return m_clickAccepted;
        }

        // 0x06000358: the base transition is always asked for a non-instant change (edx cleared at
        // 0x1B52D73); only the animations started here receive the caller's instant flag.
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, false);
            switch (state)
            {
                case SelectionState.Normal:
                    if (m_previousSelectionState == SelectionState.Highlighted && m_currentAnimatingState != AnimationState.AnimatingRelease)
                    {
                        StopAnimationCoroutine();
                        ResetTransformToCache();
                    }
                    break;
                case SelectionState.Highlighted:
                    if (m_currentAnimatingState != AnimationState.AnimatingDisabled && m_previousSelectionState == SelectionState.Pressed &&
                        gameObject.activeInHierarchy && !m_animationDisabled)
                    {
                        StopAnimationCoroutine();
                        m_animatingCoroutine = StartCoroutine(AnimateOnRelease(instant));
                    }
                    break;
                case SelectionState.Pressed:
                    if (m_currentAnimatingState != AnimationState.AnimatingDisabled && !m_onCooldown)
                    {
                        StopAnimationCoroutine();
                        if (gameObject.activeInHierarchy && !m_animationDisabled)
                        {
                            m_animatingCoroutine = StartCoroutine(AnimateOnPress(instant));
                            m_clickAccepted = false;
                        }
                    }
                    break;
                case SelectionState.Disabled:
                    if (m_previousSelectionState != SelectionState.Disabled)
                    {
                        if (m_clickAccepted)
                        {
                            StopAnimationCoroutine();
                            if (gameObject.activeInHierarchy && !m_animationDisabled)
                            {
                                m_animatingCoroutine = StartCoroutine(AnimateOnRelease(instant));
                                break;
                            }
                        }
                        ResetTransformToCache();
                    }
                    break;
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (Application.isPlaying && TargetTransform != null)
                CacheLocalTransform();
        }

        // The three animations always apply one step before their first yield, so an instant
        // animation still runs a single frame. A NaN timer or end time ends the loop.
        protected IEnumerator AnimateOnPress(bool instant = false)
        {
            m_currentAnimatingState = AnimationState.AnimatingPress;
            InvokeEvent(UIEventType.OnPress);
            float endTime = Mathf.Max(m_curveCollection.OnPressedXCurve.EndTime(), m_curveCollection.OnPressedYCurve.EndTime());
            float timer = instant ? endTime : 0f;
            do
            {
                timer += Time.unscaledDeltaTime;
                float x = m_curveCollection.OnPressedXCurve.SampleAtTime(timer);
                float y = m_curveCollection.OnPressedYCurve.SampleAtTime(timer);
                TargetTransform.localScale = new Vector3(x, y, 1f);
                yield return null;
            } while (timer < endTime);
            m_currentAnimatingState = AnimationState.NotAnimating;
            m_animatingCoroutine = null;
        }

        protected IEnumerator AnimateOnRelease(bool instant = false)
        {
            m_currentAnimatingState = AnimationState.AnimatingRelease;
            InvokeEvent(UIEventType.OnRelease);
            float endTime = Mathf.Max(m_curveCollection.OnReleasedXCurve.EndTime(), m_curveCollection.OnReleasedYCurve.EndTime());
            float timer = instant ? endTime : 0f;
            do
            {
                timer += Time.unscaledDeltaTime;
                float x = m_curveCollection.OnReleasedXCurve.SampleAtTime(timer);
                float y = m_curveCollection.OnReleasedYCurve.SampleAtTime(timer);
                TargetTransform.localScale = new Vector3(x, y, 1f);
                yield return null;
            } while (timer < endTime);
            m_currentAnimatingState = AnimationState.NotAnimating;
            m_animatingCoroutine = null;
        }

        // The shake offsets the position cached here; the restart request is honoured only after a yield.
        protected IEnumerator AnimateOnPressDisabled(bool instant = false)
        {
            m_currentAnimatingState = AnimationState.AnimatingDisabled;
            InvokeEvent(UIEventType.OnDisabled);
            m_cachedLocalPosition = TargetTransform.localPosition;
            float endTime = Mathf.Max(m_curveCollection.OnDisabledXCurve.EndTime(), m_curveCollection.OnDisabledYCurve.EndTime());
            float timer = instant ? endTime : 0f;
            Vector2 scaler = m_curveCollection.OnDisabledAnimDirection;
            do
            {
                timer += Time.unscaledDeltaTime;
                float x = m_curveCollection.OnDisabledXCurve.SampleAtTime(timer);
                float y = m_curveCollection.OnDisabledYCurve.SampleAtTime(timer);
                TargetTransform.localPosition = m_cachedLocalPosition + new Vector3(x * scaler.x, y * scaler.y, 0f);
                yield return null;
                if (m_shouldResetInactiveAnim)
                {
                    timer = 0f;
                    m_shouldResetInactiveAnim = false;
                }
            } while (timer < endTime);
            TargetTransform.localPosition = m_cachedLocalPosition;
            m_currentAnimatingState = AnimationState.NotAnimating;
            m_animatingCoroutine = null;
        }

        // Stopping this coroutine early leaves the global cooldown set (its Dispose is empty).
        private IEnumerator GlobalCooldownClicks()
        {
            s_globalInputOnCooldown = true;
            yield return CoroutineUtils.WaitForRealSeconds(m_cooldownTimeInSeconds);
            s_globalInputOnCooldown = false;
        }

        protected void StopAnimationCoroutine()
        {
            this.SafeStopCoroutine(ref m_animatingCoroutine);
            m_currentAnimatingState = AnimationState.NotAnimating;
        }

        // Clears the flag only; a running cooldown coroutine still clears it again when it ends.
        public static void CancelGlobalInputCooldown()
        {
            s_globalInputOnCooldown = false;
        }

        public void RegisterButtonStatusEffector(ButtonStatusEffects effector)
        {
            if (m_buttonStatusEffects.Contains(effector))
                return;
            m_buttonStatusEffects.Add(effector);
            m_hasStatusEffects = m_buttonStatusEffects.Count > 0;
            TriggerButtonStatusAffectors();
        }

        protected void TriggerButtonStatusAffectors()
        {
            if (!m_hasStatusEffects)
                return;
            for (int i = 0; i < m_buttonStatusEffects.Count; i++)
            {
                if (Interactable)
                    m_buttonStatusEffects[i].SetEnabledButtonState();
                else
                    m_buttonStatusEffects[i].SetDisabledButtonState();
            }
        }
    }
}
