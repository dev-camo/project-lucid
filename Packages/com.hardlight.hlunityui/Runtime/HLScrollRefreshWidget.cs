// Original HLUnityUI.Runtime HLScrollRefreshWidget 02000010 (global namespace) and its
// nested ActivateEvent 02000011, reconstructed from the x86-64 bodies
// (0x1B3BEA0..0x1B3C600) and metadata. ActivateEvent is declared first: as member 0 it
// makes OnEndDrag member ordinal 19, which names the lambda <OnEndDrag>b__19_0. No
// shipped .prefab/.unity/.asset in the 2026-10-05 reference export references the
// exported script GUID 2bf0398540ff297e912a1e4b41545f15, so the script stays unbound
// with a fresh GUID.
using System;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;

public class HLScrollRefreshWidget : MonoBehaviour
{
    // The listener receives a completion callback to call once the refresh finishes.
    [Serializable]
    public class ActivateEvent : UnityEvent<Action<bool>>
    {
    }

    // Only OnActivate has an initializer (0x06000082); OnCancel and OnComplete have none.
    public ActivateEvent OnActivate = new ActivateEvent();
    public UnityEvent OnCancel;
    public UnityEvent OnComplete;
    [SerializeField]
    private HLScrollRect m_scrollRect;
    [UnityEngine.Range(0f, 1f)]
    [SerializeField]
    private float m_dragThreshold = 0.15f;
    [SerializeField]
    [UnityEngine.Range(0f, 1f)]
    private float m_activationPercentage = 0.6f;
    [SerializeField]
    private Animation m_animation;
    [SerializeField]
    private string m_animationNamePull;
    [SerializeField]
    private string m_animationNameLoad;
    [SerializeField]
    private string m_animationNameEnd;
    private float m_progress;
    private AnimationState m_animationState;
    private bool m_refreshing;

    public bool Refreshing => m_refreshing;

    private void Awake()
    {
        if (m_scrollRect != null)
        {
            m_scrollRect.OnDragReleased += OnEndDrag;
            m_scrollRect.OnRubberDrag += OnDrag;
        }
    }

    private void OnDestroy()
    {
        if (m_scrollRect != null)
        {
            m_scrollRect.OnDragReleased -= OnEndDrag;
            m_scrollRect.OnRubberDrag -= OnDrag;
        }
    }

    // 0x0600007f: progress is the rubber-band stretch over the threshold, clamped to
    // [0, 1]. It is stored even without an animation, and is not gated on m_refreshing.
    public void OnDrag(float progress)
    {
        m_progress = Mathf.Clamp01(progress / m_dragThreshold);
        if (m_animation != null)
        {
            m_animation.Stop();
            SetupAnimation(m_animationNamePull, 0, WrapMode.Default);
            m_animationState.normalizedTime = m_progress;
        }
    }

    // 0x06000080 (jae at 0x1B3C3DA): a NaN progress takes the cancel branch, which rewinds
    // the pull animation at speed -1 from the current progress. Neither branch checks for
    // an animation.
    public void OnEndDrag()
    {
        if (m_progress >= m_activationPercentage)
        {
            SetupAnimation(m_animationNameLoad, 1, WrapMode.Loop);
            m_animation.Play();
            m_refreshing = true;
            OnActivate.Invoke(value =>
            {
                m_refreshing = false;
                OnComplete.Invoke();
                SetupAnimation(m_animationNameEnd, 1, WrapMode.Default);
                m_animation.Play();
            });
        }
        else
        {
            SetupAnimation(m_animationNamePull, -1, WrapMode.Default);
            m_animationState.normalizedTime = m_progress;
            m_animation.Play();
            m_progress = 0f;
            OnCancel.Invoke();
        }
    }

    private void SetupAnimation(string animnName, int speed, WrapMode wrapMode)
    {
        m_animationState = m_animation[animnName];
        m_animationState.enabled = true;
        m_animationState.weight = 1f;
        m_animationState.speed = speed;
        m_animationState.wrapMode = wrapMode;
        m_animation.clip = m_animationState.clip;
    }
}
