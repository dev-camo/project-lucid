using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 02000031, all twenty own APIs plus the genuine five-method nested record.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(Animation))]
    [RequireComponent(typeof(UIInteractable))]
    [RequireComponent(typeof(RectTransform))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UITransitionAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private List<UITransitionAnimationSetting> m_settings;
        [SerializeField] [InspectorReadOnly] private UIInteractable m_uiInteractable;
        private readonly Queue<AnimationElement> m_animationsQueue = new Queue<AnimationElement>();
        private AnimationElement m_currentAnimationElement;
        private AnimationElement m_fallbackAnimationElement;
        private bool m_isAwake;

        // Original nested 02000032; all five APIs and the original ordered readonly backing fields.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private sealed class AnimationElement
        {
            public AnimationClip AnimationClip { get; }
            public Action OnComplete { get; }
            public UITransitionState State { get; }
            public AnimationElement(AnimationClip animationClip, Action onComplete)
            {
                AnimationClip = animationClip;
                OnComplete = onComplete;
                State = UITransitionState.Normal;
            }
            public AnimationElement(UITransitionAnimationSetting setting)
            {
                AnimationClip = setting.AnimationClip;
                OnComplete = null;
                State = setting.State;
            }
        }

        // Original 06000160: a default-state fault leaves the awake flag false.
        private void Awake() { Default(); m_isAwake = true; }
        // Original 06000161: stopping the animation precedes clearing the pending queue.
        private void OnDisable() { m_animation.Stop(); m_animationsQueue.Clear(); }
        // Original 06000162/163: event-data and current/fallback state access precede interactability validation.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) ValidateAndForceTransition(UITransitionState.Pressed);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && m_currentAnimationElement.State == UITransitionState.Pressed)
                ValidateAndForceTransition(m_fallbackAnimationElement.State);
        }
        // Original 06000164/165: exit captures the fallback object rather than only its state.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) ValidateAndForceTransition(UITransitionState.Highlighted);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && m_currentAnimationElement.State == UITransitionState.Highlighted)
                ValidateAndForceTransition(m_fallbackAnimationElement);
        }

        // Original 06000166: two distinct records and callbacks share the original onComplete capture.
        public void QueueCustomAnimation(AnimationClip animationClip, Action onComplete = null)
        {
            m_fallbackAnimationElement = new AnimationElement(animationClip, () => OnCustomAnimationComplete(onComplete));
            m_currentAnimationElement = new AnimationElement(animationClip, () => OnCustomAnimationComplete(onComplete));
            m_animationsQueue.Enqueue(m_currentAnimationElement);
            TryPlayNextAnimation();
        }
        // Original 06000167: fallback then current are published before enqueuing and trying playback.
        public void QueueTransitionToState(UITransitionState state)
        {
            var setting = GetSettingByState(state);
            if (setting == null) return;
            m_fallbackAnimationElement = new AnimationElement(setting);
            m_currentAnimationElement = new AnimationElement(setting);
            m_animationsQueue.Enqueue(m_currentAnimationElement);
            TryPlayNextAnimation();
        }
        // Original 06000168: a callback fault prevents trying the next animation.
        public void Action_AnimationEnd() { m_currentAnimationElement?.OnComplete?.Invoke(); TryPlayNextAnimation(); }
        // Original 06000169; the natural callbacks 0600017a/17b inline this optional invocation.
        private static void OnCustomAnimationComplete(Action onComplete) { onComplete?.Invoke(); }
        // Original 0600016a: restore before dequeuing; no active or currently-playing guard is introduced.
        private void TryPlayNextAnimation()
        {
            if (m_animationsQueue.Count <= 0) return;
            RestoreAnimation();
            m_currentAnimationElement = m_animationsQueue.Dequeue();
            m_animation.Play(m_currentAnimationElement.AnimationClip.name);
        }
        // Original 0600016b/16c: restore, publish current, clear queue, then dereference/play the new clip.
        private void ForceTransition(UITransitionState state)
        {
            var setting = GetSettingByState(state);
            if (setting != null) ForceTransition(new AnimationElement(setting));
        }
        private void ForceTransition(AnimationElement animationElement)
        {
            RestoreAnimation();
            m_currentAnimationElement = animationElement;
            m_animationsQueue.Clear();
            m_animation.Play(m_currentAnimationElement.AnimationClip.name);
        }
        // Original 0600016d: sampling is gated only by the awake flag, with no finally around Stop.
        private void RestoreAnimation()
        {
            m_animation.Rewind();
            m_animation.Play(m_currentAnimationElement.AnimationClip.name);
            if (m_isAwake) m_animation.Sample();
            m_animation.Stop();
        }
        // Original 0600016e/16f: IsActive dispatch precedes a fresh nonvirtual IsInteractable check.
        private void ValidateAndForceTransition(UITransitionState state)
        {
            if (m_uiInteractable.IsActive() && m_uiInteractable.IsInteractable()) ForceTransition(state);
        }
        private void ValidateAndForceTransition(AnimationElement animationElement)
        {
            if (m_uiInteractable.IsActive() && m_uiInteractable.IsInteractable()) ForceTransition(animationElement);
        }
        // Original 06000170: default initialization publishes current first and fallback second without playback.
        private void Default()
        {
            var setting = GetSettingByState(UITransitionState.Normal);
            if (setting == null) return;
            m_currentAnimationElement = new AnimationElement(setting);
            m_fallbackAnimationElement = new AnimationElement(setting);
        }
        // Original 06000171: first live-list match; null entries fault and the enumerator is still disposed.
        private UITransitionAnimationSetting GetSettingByState(UITransitionState state)
        {
            foreach (var setting in m_settings) if (setting.State == state) return setting;
            return null;
        }
        // Original 06000172: interactable lookup and publication precede animation lookup.
        private void Reset() { m_uiInteractable = GetComponent<UIInteractable>(); m_animation = GetComponent<Animation>(); }
        // Original 06000173, only the queue is initialized before the MonoBehaviour constructor.
        public UITransitionAnimation() { }

    }
}
