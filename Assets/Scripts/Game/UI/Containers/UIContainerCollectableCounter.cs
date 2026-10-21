using System.Collections;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000892 and 02000893 (iterator); bodies 0x624890..0x625170 (1.10.1 x86-64).
    // A collectable counter fed either from live gameplay state or from the save, with the count
    // ticking up at the start of a mission.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerCollectableCounter : UIContainer
    {
        [SerializeField]
        private CollectableType m_type;
        [SerializeField]
        [HashEnum(null)]
        [Tooltip("If set, overrides the formatter string from the collectable definition.")]
        private Strings m_formatStringOverride = Strings.NONE;
        [SerializeField]
        protected UnityEvent<int> m_onPositiveChange;
        [SerializeField]
        protected UnityEvent<int> m_onNegativeChange;
        [SerializeField]
        [Tooltip("The rate that ring count fills to target on entering a level.")]
        private float m_ringCountRate = 0.01f;
        public readonly Bindable<int> TotalAmount = new Bindable<int>();
        public readonly Bindable<int> CollectedAmount = new Bindable<int>();
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<bool> HasValues = new Bindable<bool>();
        public readonly Bindable<string> DisplayFormattedString = new Bindable<string>();
        private readonly SystemRef<ProgressionManager> m_progressionManagerRef = ProcessManager.GetSystemRef<ProgressionManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private UIContainerCollectableCounterParameters m_parameters;
        private int m_collectedTarget;
        private Coroutine m_collectedCoroutine;

        // 0600318d
        public CollectableType Type => m_type;

        // 0600318e: the format string comes from the override unless it is NONE, then the display
        // refreshes from gameplay state or the save.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerCollectableCounterParameters>();
            DisplayFormattedString.Value = m_formatStringOverride == Strings.NONE
                ? m_parameters.Definition.DisplayFormattedString
                : StringTable.GetString(m_formatStringOverride);
            Icon.Value = m_parameters.Definition.LoadIcon();
            if (m_parameters.InGameplay)
                UpdateFromGameplay();
            else
                UpdateFromSave();
        }

        // 0600318f
        private void UpdateFromGameplay()
        {
            if (m_parameters.CollectableExists)
                OnCollectableStateUpdate(m_parameters.State, default);
            else
                HasValues.Value = false;
        }

        // 06003190: the spent count is the blue coin save data's, whatever the counter's type.
        private void UpdateFromSave()
        {
            ProgressionCollectable progress = m_progressionManagerRef.Get().GetTotalProgress(m_parameters.Definition.Type);
            int spent = m_saveManagerRef.Get().CurrentSave.BlueCoins.Spent;
            TotalAmount.Value = progress.Total;
            CollectedAmount.Value = unchecked(progress.Collected - spent);
            HasValues.Value = true;
        }

        // 06003191 / MoveNext 06003197: restart the count from zero and add one every m_ringCountRate seconds
        // until it reaches the target, which can move while the count runs.
        private IEnumerator LerpToCollectedTarget(int collectedTarget)
        {
            CollectedAmount.Value = 0;
            m_collectedTarget = collectedTarget;
            float timer = m_ringCountRate;
            while (CollectedAmount.Value < m_collectedTarget)
            {
                timer -= Time.deltaTime;
                if (timer > 0f)
                {
                    yield return null;
                    continue;
                }
                timer += m_ringCountRate;
                CollectedAmount.Value++;
                yield return null;
            }
            CollectedAmount.Value = m_collectedTarget;
            m_collectedCoroutine = null;
        }

        // 06003192: a mission start counts up to the collected amount; otherwise a running count
        // retargets and an idle one jumps. The change events carry the signed difference.
        public virtual void OnCollectableStateUpdate(CollectableState collectableState, CollectableChangeMetadata metadata)
        {
            int previousCollected = CollectedAmount.Value;
            TotalAmount.Value = collectableState.Total;
            HasValues.Value = true;
            if (metadata.Source == CollectableSource.MissionStart)
                m_collectedCoroutine = StartCoroutine(LerpToCollectedTarget(collectableState.Collected));
            else if (m_collectedCoroutine != null)
                m_collectedTarget = collectableState.Collected;
            else
                CollectedAmount.Value = collectableState.Collected;
            int change = CollectedAmount.Value - previousCollected;
            if (change > 0)
            {
                if (m_onPositiveChange != null)
                    m_onPositiveChange.Invoke(change);
            }
            else if (CollectedAmount.Value != previousCollected && m_onNegativeChange != null)
            {
                m_onNegativeChange.Invoke(change);
            }
        }

        // 06003193
        public void Refresh()
        {
            if (m_parameters.InGameplay)
                UpdateFromGameplay();
            else
                UpdateFromSave();
        }

        // 06003194
        public UIContainerCollectableCounter() { }
    }
}
