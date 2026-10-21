using Hardlight;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200089f and 020008a0 (closure); bodies 0x629530..0x629F50 (1.10.1 x86-64).
    // The corruption meter: it registers itself with the UI manager, plays an intro when enabled and
    // switches between the hit, normal and outro animations as the progress changes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerCorruption : UIContainer, IUIContainerProgress
    {
        [SerializeField]
        private GameObject m_progressRoot;
        [SerializeField]
        private Animation m_animation;
        [SerializeField]
        private AnimationClip m_clipIntro;
        [SerializeField]
        private AnimationClip m_clipHit;
        [SerializeField]
        private AnimationClip m_clipNormal;
        [SerializeField]
        private AnimationClip m_clipOutro;
        public readonly Bindable<float> CorruptionProgress = new Bindable<float>();
        public readonly Bindable<int> CorruptionCurrent = new Bindable<int>();
        public readonly Bindable<int> CorruptionTotal = new Bindable<int>();
        private static readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>(null, true);

        // 060031d2
        private void Awake()
        {
            m_uiManagerRef.InvokeOnValid(uiManager => uiManager.Register(this));
            m_progressRoot.SetActive(false);
        }

        // 060031d3
        private void OnDestroy()
        {
            if (m_uiManagerRef.TryGet(out UIManager uiManager))
                uiManager.Unregister(this);
        }

        // 060031d4: enabling shows the meter from zero and plays the intro; disabling plays on until the
        // current clip has finished (or stopped) and then hides the meter.
        public override void SetEnabled(bool enabled)
        {
            base.SetEnabled(enabled);
            if (m_progressRoot == null)
                return;
            if (enabled)
            {
                m_progressRoot.SetActive(true);
                CorruptionProgress.Value = 0f;
                CorruptionCurrent.Value = 0;
                CorruptionTotal.Value = 0;
                if (m_clipIntro == null)
                    return;
                m_animation.clip = m_clipIntro;
                m_animation.Play();
            }
            else
            {
                AnimationState animationState = m_animation[m_animation.clip.name];
                CoroutineUtils.OnPredicateActive(
                    () => m_progressRoot.SetActive(false),
                    () => !animationState.enabled || Mathf.Approximately(animationState.time, animationState.length));
            }
        }

        // 060031d5: a hidden meter is enabled instead. An unchanged progress, or an intro that has not
        // finished, changes nothing. Zero plays the hit clip, full progress the outro and anything
        // else the normal clip unless it is already the current one.
        public void SetProgress(float progress)
        {
            float previousProgress = CorruptionProgress.Value;
            CorruptionProgress.Value = progress;
            if (!m_progressRoot.activeSelf)
            {
                SetEnabled(true);
                return;
            }
            if (Mathf.Approximately(progress, previousProgress))
                return;
            if (m_animation.clip == m_clipIntro && m_animation[m_animation.clip.name].normalizedTime < 1f)
                return;
            if (progress == 0f)
            {
                PlayAnimation(m_clipHit);
            }
            else if (Mathf.Approximately(progress, 1f))
            {
                PlayAnimation(m_clipOutro);
            }
            else if (m_animation.clip != m_clipNormal)
            {
                PlayAnimation(m_clipNormal);
            }
        }

        // 060031d6
        private void PlayAnimation(AnimationClip clip)
        {
            if (clip == null)
                return;
            m_animation.clip = clip;
            m_animation.Play();
        }

        // 060031d7
        public void SetProgress(int current, int total)
        {
            CorruptionCurrent.Value = current;
            CorruptionTotal.Value = total;
        }

        // 060031d8
        public UIContainerCorruption() { }
    }
}
