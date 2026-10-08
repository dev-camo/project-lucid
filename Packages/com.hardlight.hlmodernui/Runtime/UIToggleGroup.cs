using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime owner 0200001e; its genuine seven cache/lambda APIs remain compiler-binding held.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hardlight/HLModernUI/UIToggleGroup")]
    [RequireComponent(typeof(RectTransform))]
    public class UIToggleGroup : UIBehaviour
    {
        [SerializeField] private bool m_allowSwitchOff;
        [Tooltip("By default, you can call SelectPrevious and SelectNext and this will cycle through from 0 to Count and vice-versa.")]
        [SerializeField] private bool m_cycle = true;
        // Original 0600008b/8c.
        public bool AllowSwitchOff { get => m_allowSwitchOff; set => m_allowSwitchOff = value; }
        public event Action OnGroupChanged = () => { };
        private readonly List<UIToggleGroupElement> m_toggles = new List<UIToggleGroupElement>();

        // Original 0600008f.
        public int ToggleCount => m_toggles.Count;

        // Original 06000090: validation precedes the inherited lifecycle callback.
        protected override void Start() { EnsureValidState(); base.Start(); }
        // Original 06000091.
        protected override void OnEnable() { EnsureValidState(); base.OnEnable(); }

        // Original 06000092: Unity equality precedes List.Contains; the exact literal and three-object format are retained.
        private void ValidateToggleIsInGroup(UIToggleGroupElement toggle)
        {
            if (toggle == null || !m_toggles.Contains(toggle))
                throw new ArgumentException(string.Format("{0} {1} is not part of ToggleGroup {2}", "UIToggleGroupElement", toggle, this));
        }

        // Original 06000093; live-list foreach disposal remains on both ordinary and exceptional exits.
        public void NotifyToggleOn(UIToggleGroupElement toggle, bool sendCallback = true)
        {
            ValidateToggleIsInGroup(toggle);
            foreach (var element in m_toggles)
            {
                if (element == toggle) continue;
                if (sendCallback) element.Toggle.IsOn = false;
                else element.Toggle.SetIsOnWithoutNotify(false);
            }
        }

        // Original 06000094: mutation precedes the listener lookup/removal and direct group callback.
        public void UnregisterToggle(UIToggleGroupElement toggle)
        {
            if (!m_toggles.Contains(toggle)) return;
            m_toggles.Remove(toggle);
            toggle.Toggle.OnValueChanged.RemoveListener(OnAnyToggleValueChanged);
            OnGroupChanged();
        }

        // Original 06000095: a null element is added before its genuine provider access faults.
        public void RegisterToggle(UIToggleGroupElement toggle)
        {
            if (m_toggles.Contains(toggle)) return;
            m_toggles.Add(toggle);
            toggle.Toggle.OnValueChanged.AddListener(OnAnyToggleValueChanged);
            OnGroupChanged();
        }

        // Original 06000096; the bool parameter is intentionally unused.
        private void OnAnyToggleValueChanged(bool toggleIsOn) { OnGroupChanged(); }

        // Original 06000097: retain the fresh field/index reads after the first toggle's callbacks.
        public void EnsureValidState()
        {
            if (!m_allowSwitchOff && !AnyTogglesOn() && m_toggles.Count != 0)
            {
                m_toggles[0].Toggle.IsOn = true;
                NotifyToggleOn(m_toggles[0]);
            }
            var activeToggles = ActiveToggles();
            if (activeToggles.Count >= 2)
            {
                var firstActiveToggle = GetFirstActiveToggle();
                foreach (var element in activeToggles)
                    if (element != firstActiveToggle) element.Toggle.IsOn = false;
            }
        }

        // Original 06000098 / cache predicate 060000a3. The found Unity object's lifetime affects the comparison.
        public bool AnyTogglesOn() => m_toggles.Find(x => x.Toggle.IsOn) != null;
        // Original 06000099 / cache predicate 060000a4; FindAll returns a separate snapshot.
        public List<UIToggleGroupElement> ActiveToggles() => m_toggles.FindAll(x => x.Toggle.IsOn);

        // Original 0600009a: this authentic Core extension is used after a separate active-list query.
        public UIToggleGroupElement GetFirstActiveToggle()
        {
            var activeToggles = ActiveToggles();
            return activeToggles.Count > 0 ? ReadOnlyListExtensions.First(activeToggles) : null;
        }

        // Original 0600009b / cache predicate 060000a5.
        public int GetFirstActiveToggleIndex()
        {
            if (m_toggles.Count < 1) return -1;
            return m_toggles.FindIndex(element => element.Toggle.IsOn);
        }

        // Original 0600009c: normal completion restores the flag; a fault retains true while foreach still disposes.
        public void SetAllTogglesOff(bool sendCallback = true)
        {
            bool allowSwitchOff = m_allowSwitchOff;
            m_allowSwitchOff = true;
            foreach (var element in m_toggles)
            {
                if (sendCallback) element.Toggle.IsOn = false;
                else element.Toggle.SetIsOnWithoutNotify(false);
            }
            m_allowSwitchOff = allowSwitchOff;
        }

        // Original 0600009d/9e.
        public void SelectNext() { SelectOffsetFromActiveToggle(1); }
        public void SelectPrevious() { SelectOffsetFromActiveToggle(-1); }

        // Original 0600009f / cache predicate 060000a6. Signed remainder and unchecked arithmetic are original.
        private void SelectOffsetFromActiveToggle(int indexOffset)
        {
            int count = m_toggles.Count;
            if (count == 0) return;
            int index = unchecked(m_toggles.FindIndex(element => element.Toggle.IsOn) + indexOffset);
            if (m_cycle) index = unchecked(index + count) % count;
            else index = Mathf.Clamp(index, 0, m_toggles.Count - 1);
            m_toggles[index].Toggle.IsOn = true;
        }

        // Original 060000a0: initializers above execute before the inherited constructor; cache 060000a7 is empty.
        public UIToggleGroup() { }
    }
}
