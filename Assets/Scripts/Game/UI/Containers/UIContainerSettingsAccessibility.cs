using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000923; bodies translated from the 1.10.1 x86-64 binary.
    // The accessibility page: game speed, fail state, subtitles and the first-time-experience skip.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerSettingsAccessibility : UIContainerSettingsBase
    {
        public readonly Bindable<float> GameSpeed = new Bindable<float>();
        public readonly Bindable<float> GameSpeedMin = new Bindable<float>();
        public readonly Bindable<float> GameSpeedMax = new Bindable<float>();
        public readonly Bindable<string> GameSpeedPercentageString = new Bindable<string>();
        public readonly Bindable<AccessibilityFailState> FailStateType = new Bindable<AccessibilityFailState>();
        public readonly Bindable<bool> GameSpeedToggle = new Bindable<bool>();
        public readonly Bindable<bool> SubtitlesToggle = new Bindable<bool>();
        public readonly Bindable<bool> SkipFTUEToggle = new Bindable<bool>();
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
        private float m_cachedGameSpeed;
        private bool m_saveOnExit;

        // 0x060034a3 (0x6796A0)
        private void Awake()
        {
            RefreshBinders();
            m_cachedGameSpeed = GameSpeed.Value;
        }

        // 0x060034a4 (0x6796E0): the game speed slider only stores while dragged; it is saved when the page closes.
        private void OnDestroy()
        {
            if (m_saveOnExit)
                SetSaveData(_ => { });

            if (m_cachedGameSpeed != GameSpeed.Value)
                AnalyticsEventCollector.SettingChangedEvent("game_speed", m_cachedGameSpeed.ToString("0.0"), GameSpeed.Value.ToString("0.0"));
        }

        // 0x060034a5 (0x6799E0)
        protected override void RefreshBinders()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            SaveDataSettings settings = m_saveManagerRef.Get().GetSaveDataSettings();
            GameSpeed.Value = settings.GameSpeed;
            GameSpeedMin.Value = timeManager.GameSpeedMin;
            GameSpeedMax.Value = timeManager.GameSpeedMax;
            // The product is rounded to single precision before the truncation, as the native code does.
            GameSpeedPercentageString.Value = string.Format(StringTable.GetString(Strings.SETTINGS_SLIDER_LABEL_PERCENT),
                (int)(float)(settings.GameSpeed * 100f));
            FailStateType.Value = settings.FailState;
            GameSpeedToggle.Value = settings.GameSpeedToggleEnabled;
            SubtitlesToggle.Value = settings.SubtitlesEnabled;
            SkipFTUEToggle.Value = settings.SkipFTUE;
        }

        // 0x060034a6 (0x679BB0)
        protected override void ResetToDefaults(SaveDataSettings saveDataSettings)
        {
            saveDataSettings.SetAccessibilityDefaults();
            m_saveOnExit = false;
        }

        // 0x060034a7 (0x679BE0): stored without saving; OnDestroy (or DoOnAfterSave) settles it.
        public void Action_SetGameSpeed(float value)
        {
            SetSaveData(data => data.GameSpeed = value, false);
            m_saveOnExit = true;
        }

        // 0x060034a8 (0x679CF0)
        public void Action_SetSubtitlesEnabled(bool value)
        {
            SetSaveData(data => data.SubtitlesEnabled = value);
        }

        // 0x060034a9 (0x679E10)
        private void SetFailStateType(AccessibilityFailState failStateType)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("fail_state", data.FailState, failStateType);
                data.FailState = failStateType;
            });
        }

        // 0x060034aa (0x679F40)
        public void Action_SetFailStateDefault()
        {
            SetFailStateType(AccessibilityFailState.Default);
        }

        // 0x060034ab (0x679F50)
        public void Action_SetFailStateNoFail()
        {
            SetFailStateType(AccessibilityFailState.NoFail);
        }

        // 0x060034ac (0x679F60)
        public void Action_SetFailStateInvincible()
        {
            SetFailStateType(AccessibilityFailState.Invincible);
        }

        // 0x060034ad (0x679F70): switching the game speed off tells the time manager to apply it again.
        public void Action_SetGameSpeedToggleEnabled(bool value)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("game_speed_toggle_enabled", data.GameSpeedToggleEnabled, value);
                data.GameSpeedToggleEnabled = value;
                if (!value)
                    m_timeManagerRef.Get().GameSpeedApplied = true;
            });
        }

        // 0x060034ae (0x67A0A0)
        public void Action_SetSkipFTUE(bool skip)
        {
            SetSaveData(data => data.SkipFTUE = skip);
        }

        // 0x060034af (0x67A1C0)
        protected override void DoOnAfterSave()
        {
            m_saveOnExit = false;
        }

        // 0x060034b0 (0x67A1E0)
        public UIContainerSettingsAccessibility() { }
    }
}
