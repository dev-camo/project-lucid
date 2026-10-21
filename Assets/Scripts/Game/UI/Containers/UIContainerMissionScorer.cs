using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e7; bodies translated from the 1.10.1 x86-64 binary.
    // Mirrors the active mission's scorer (points, combo and streak) into bindables and score texts.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerMissionScorer : UIContainer
    {
        [Tooltip("Object to be enabled while timer is active.")]
        [SerializeField]
        private RectTransform m_enableRoot;
        [SerializeField]
        [Tooltip("Populates text UI with current score.")]
        private UITextString m_currentScoreUI = new UITextString(Strings.MISSION_SCORER_CURRENT_SCORE);
        [SerializeField]
        [Tooltip("Populates text UI with added score.")]
        private UITextString m_addedScoreUI = new UITextString(Strings.MISSION_SCORER_ADDED_SCORE);
        [SerializeField]
        [Tooltip("Populates text UI with added score from streak.")]
        private UITextString m_addedStreakUI = new UITextString(Strings.MISSION_SCORER_ADDED_SCORE);
        [SerializeField]
        [Tooltip("Populates text UI with current multiplier.")]
        private UITextString m_currentMultiplierUI = new UITextString(Strings.MISSION_SCORER_CURRENT_MULTIPLIER);
        public readonly Bindable<int> PointsTotal = new Bindable<int>();
        public readonly Bindable<int> PointsAdded = new Bindable<int>();
        public readonly Bindable<int> PointsAddedRaw = new Bindable<int>();
        public readonly Bindable<int> PointsLost = new Bindable<int>();
        public readonly Bindable<bool> ShowPointsAdded = new Bindable<bool>();
        public readonly Bindable<int> PointsTotalThisCombo = new Bindable<int>();
        public readonly Bindable<bool> PointsAddedFromCombo = new Bindable<bool>();
        public readonly Bindable<int> PointsTotalThisStreak = new Bindable<int>();
        public readonly Bindable<bool> PointsAddedFromStreak = new Bindable<bool>();
        public readonly Bindable<bool> ComboActive = new Bindable<bool>();
        public readonly Bindable<float> ComboMultiplier = new Bindable<float>();
        public readonly Bindable<float> ComboStageMultiplier = new Bindable<float>();
        public readonly Bindable<float> ComboTimeout = new Bindable<float>();
        public readonly Bindable<float> ComboTimeoutNormalised = new Bindable<float>();
        public readonly Bindable<int> ComboCount = new Bindable<int>();
        public readonly Bindable<bool> ComboAtMax = new Bindable<bool>();
        public readonly Bindable<bool> ComboSuspended = new Bindable<bool>();
        private MissionScorer m_missionScorer;
        private bool m_initialised;
        private bool m_delayShowingPoints = true;
        private bool m_hasMissionScorer;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 06003361 (0x6409B0): the scorer is hidden until the active mission is set up again.
        private void OnEnable()
        {
            m_initialised = false;
            m_delayShowingPoints = true;
            m_enableRoot.gameObject.SetActive(false);
        }

        // 06003362 (0x640A10): the first frame after enabling copies the values without the added-points flags,
        // which only start following the scorer from the second frame.
        private void Update()
        {
            if (!TryInitialise())
                return;
            if (!m_hasMissionScorer)
            {
                m_enableRoot.gameObject.SetActive(false);
                return;
            }
            PointsTotal.Value = m_missionScorer.PointsTotal;
            PointsAdded.Value = m_missionScorer.PointsAdded;
            PointsAddedRaw.Value = m_missionScorer.PointsAddedRaw;
            PointsLost.Value = m_missionScorer.PointsLost;
            PointsTotalThisCombo.Value = m_missionScorer.PointsTotalThisCombo;
            PointsAddedFromCombo.Value = m_missionScorer.PointsAddedFromCombo;
            PointsTotalThisStreak.Value = m_missionScorer.PointsTotalThisStreak;
            if (m_delayShowingPoints)
            {
                m_delayShowingPoints = false;
            }
            else
            {
                ShowPointsAdded.Value = m_missionScorer.ShowPointsAdded;
                PointsAddedFromStreak.Value = m_missionScorer.PointsAddedFromStreak;
            }
            ComboActive.Value = m_missionScorer.ComboActive;
            ComboMultiplier.Value = m_missionScorer.ComboMultiplier;
            ComboStageMultiplier.Value = m_missionScorer.ComboStageMultiplier;
            ComboTimeout.Value = m_missionScorer.ComboTimeout;
            ComboTimeoutNormalised.Value = m_missionScorer.ComboTimeoutNormalised;
            ComboCount.Value = m_missionScorer.ComboCount;
            ComboAtMax.Value = m_missionScorer.ComboAtMax;
            ComboSuspended.Value = m_missionScorer.ComboSuspended;
            m_currentScoreUI.Apply(PointsTotal.Value);
            m_addedScoreUI.Apply(PointsAdded.Value);
            m_addedStreakUI.Apply(PointsTotalThisStreak.Value);
            m_currentMultiplierUI.Apply((float)(ComboStageMultiplier.Value * ComboMultiplier.Value));
        }

        // 06003363 (0x641100)
        private void ShowPoints()
        {
            if (m_delayShowingPoints)
            {
                m_delayShowingPoints = false;
                return;
            }
            ShowPointsAdded.Value = m_missionScorer.ShowPointsAdded;
            PointsAddedFromStreak.Value = m_missionScorer.PointsAddedFromStreak;
        }

        // 06003364 (0x6409E0)
        private void ClearValues(bool scorerVisible)
        {
            m_enableRoot.gameObject.SetActive(scorerVisible);
        }

        // 06003365 (0x640F70): waits for a set-up active mission and loaded strings. A scored mission supplies its
        // scorer. The texts are initialised and the root shown either way.
        private bool TryInitialise()
        {
            if (m_initialised)
                return true;
            if (!m_missionManagerRef.IsValid())
                return false;
            if (!m_missionManagerRef.Get().TryGetActiveMissionState(out MissionState missionState))
                return false;
            if (!missionState.SetUpComplete)
                return false;
            if (!StringTable.AreStringsLoaded())
                return false;
            if (missionState.Tracker.IsScored())
            {
                m_missionScorer = missionState.Tracker.OptionalMissionScorer;
                m_hasMissionScorer = true;
            }
            m_currentScoreUI.Initialise();
            m_addedScoreUI.Initialise();
            m_addedStreakUI.Initialise();
            m_currentMultiplierUI.Initialise();
            m_enableRoot.gameObject.SetActive(true);
            m_initialised = true;
            return true;
        }

        // 06003366
        public UIContainerMissionScorer() { }
    }
}
