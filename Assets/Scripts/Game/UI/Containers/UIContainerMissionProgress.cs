using System.Collections;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e0; bodies translated from the 1.10.1 x86-64 binary.
    // The HUD view of an instanced mission's objective count: shows it, then fades it out unless always displayed.
    // The compiler-generated Hide iterator (<Hide>d__24) follows member order; the InstancedTracker backing field sits between the serialized fields and the coroutine field.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMissionProgress : UIContainer
    {
        public readonly Bindable<Sprite> MissionIcon = new Bindable<Sprite>();
        public readonly Bindable<string> MissionProgress = new Bindable<string>();
        public readonly Bindable<int> ObjectivesComplete = new Bindable<int>();
        public readonly Bindable<int> ObjectivesTarget = new Bindable<int>();
        [SerializeField] private Strings m_localisedStringFormat = Strings.FORMAT_X_OF_Y;
        [SerializeField] private CanvasGroup m_canvasGroup;
        [SerializeField] private bool m_alwaysDisplay;
        [HideIf("m_alwaysDisplay", null)]
        [SerializeField]
        private float m_displaySeconds = 2f;
        [SerializeField]
        [HideIf("m_alwaysDisplay", null)]
        private float m_fadeTimeSeconds = 1f;
        [SerializeField] private bool m_onlyShowWhenMissionIslandActive;

        // 0600333d (0x63E740)
        public bool AlwaysDisplay => m_alwaysDisplay;

        // 0600333e (0x63E750)
        public bool OnlyShowWhenMissionIslandActive => m_onlyShowWhenMissionIslandActive;

        // 0600333f / 06003340
        public MissionInstancedTracker InstancedTracker { get; private set; }

        private Coroutine m_hideUICoroutine;
        private MissionManager m_missionManager;

        // 06003341 (0x63E790)
        private void OnEnable()
        {
            if (m_alwaysDisplay)
                return;
            m_canvasGroup.alpha = 0f;
        }

        // 06003342 (0x63E7B0)
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            InstancedTracker = parameters.GetAs<UIContainerMissionProgressParameters>().InstancedTracker;
            Refresh();
        }

        // 06003343 (0x635600): a mission progress icon replaces the "x of y" text, and is shown only while the
        // objectives are incomplete.
        public void Refresh()
        {
            gameObject.SetActive(true);
            int complete = InstancedTracker.ObjectivesComplete;
            int target = InstancedTracker.ObjectivesTarget;
            Sprite icon;
            if (InstancedTracker.Icon != null)
                icon = InstancedTracker.Icon;
            else
                icon = InstancedTracker.Definition.MissionObjectiveIcon;
            Sprite progressIcon = InstancedTracker.Definition.MissionProgressIcon;
            if (progressIcon != null)
            {
                MissionProgress.Value = string.Empty;
                if (complete < target)
                    icon = progressIcon;
            }
            else
            {
                MissionProgress.Value = string.Format(StringTable.GetString(m_localisedStringFormat), complete.ToString(), target.ToString());
            }
            MissionIcon.Value = icon;
            ObjectivesComplete.Value = complete;
            ObjectivesTarget.Value = target;
            AnimateHide();
        }

        // 06003344 (0x63E820)
        private void AnimateHide()
        {
            if (m_alwaysDisplay)
                return;
            m_canvasGroup.alpha = 1f;
            this.SafeStopCoroutine(ref m_hideUICoroutine);
            m_hideUICoroutine = StartCoroutine(Hide());
        }

        // 06003345 (0x63E8E0), state machine MoveNext at 0x63EAD0: stays visible for the display time, then fades the
        // group out and deactivates the container. Float casts pin the single-precision rounding of the native body.
        private IEnumerator Hide()
        {
            float displayUntilTime = (float)(Time.time + m_displaySeconds);
            while (displayUntilTime > Time.time)
                yield return null;
            float fadeOutValue = 1f;
            while (fadeOutValue > 0f)
            {
                m_canvasGroup.alpha = fadeOutValue;
                yield return null;
                fadeOutValue = (float)(fadeOutValue - (float)(Time.deltaTime / m_fadeTimeSeconds));
            }
            m_canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        // 06003346
        public UIContainerMissionProgress() { }
    }
}
