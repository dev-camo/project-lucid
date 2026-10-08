using UnityEngine;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 02000020, complete eight own APIs.
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(UIToggle))]
    [RequireComponent(typeof(RectTransform))]
    public class UIToggleGroupElement : UIBehaviour
    {
        [SerializeField] private UIToggleGroup m_group;
        [SerializeField] [InspectorReadOnly] private UIToggle m_toggle;

        // Original 060000a8/a9.
        public UIToggleGroup Group { get => m_group; set => SetToggleGroup(value, true); }
        // Original 060000aa.
        public UIToggle Toggle => m_toggle;

        // Original 060000ab: group validation occurs before the inherited callback, without unregistering here.
        protected override void OnDestroy()
        {
            if (m_group != null) m_group.EnsureValidState();
            base.OnDestroy();
        }

        // Original 060000ac.
        protected override void OnEnable() { base.OnEnable(); SetToggleGroup(m_group, false); }
        // Original 060000ad: unregistering leaves the serialized membership field intact.
        protected override void OnDisable() { SetToggleGroup(null, false); base.OnDisable(); }

        // Original 060000ae: retain independent Unity lifetime checks and callback-dependent active/field rereads.
        private void SetToggleGroup(UIToggleGroup newGroup, bool setMemberValue)
        {
            if (m_group != null) m_group.UnregisterToggle(this);
            if (setMemberValue) m_group = newGroup;
            if (newGroup != null && IsActive()) newGroup.RegisterToggle(this);
            if (newGroup != null && m_toggle.IsOn && IsActive()) newGroup.NotifyToggleOn(this);
        }

        // Original 060000af.
        public UIToggleGroupElement() { }
    }
}
