using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008c3; bodies 0x636070..0x6363D0 (1.10.1 x86-64).
    // Cycles through a shuffled list of localised tips, one per m_showDurationSeconds of unscaled time.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerGameTips : UIContainer
    {
        [SerializeField]
        [HashEnum(typeof(Strings))]
        private List<Strings> m_tipIds;
        [SerializeField]
        private float m_showDurationSeconds;
        public readonly Bindable<string> CurrentTip = new Bindable<string>();
        private int m_currentIndex;
        private float m_showTimerSeconds;
        private readonly List<Strings> m_shuffledTipIds = new List<Strings>();

        // 0600329b
        private void OnEnable()
        {
            ShuffleTips(m_tipIds);
        }

        // 0600329c: draw the tips in random order, then restart from the first. The index is set to
        // -1 so that Refresh shows index 0, and an empty list makes Refresh divide by zero.
        private void ShuffleTips(IReadOnlyList<Strings> tipIds)
        {
            m_shuffledTipIds.Clear();
            List<Strings> remainingTipIds = new List<Strings>(tipIds);
            for (int remainingCount = remainingTipIds.Count; remainingCount > 0; remainingCount--)
            {
                int index = Random.Range(0, remainingCount);
                m_shuffledTipIds.Add(remainingTipIds[index]);
                remainingTipIds.RemoveAt(index);
            }
            m_currentIndex = -1;
            Refresh();
        }

        // 0600329d: a NaN duration refreshes every frame.
        private void Update()
        {
            m_showTimerSeconds += Time.unscaledDeltaTime;
            if (m_showTimerSeconds < m_showDurationSeconds)
                return;
            Refresh();
        }

        // 0600329e
        private void Refresh()
        {
            m_currentIndex = (m_currentIndex + 1) % m_shuffledTipIds.Count;
            m_showTimerSeconds = 0f;
            CurrentTip.Value = StringTable.GetString(m_shuffledTipIds[m_currentIndex]);
        }

        // 0600329f
        public void SetTipOverrides(IReadOnlyList<Strings> tipOverrideIds)
        {
            ShuffleTips(tipOverrideIds);
        }

        // 060032a0
        public UIContainerGameTips() { }
    }
}
