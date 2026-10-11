// Preservation reconstruction of complete original Hardlight.UIWidgetNavigation in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
// Natural <>c cached no-op event lambda and bound WaitForUI callback stay in the whole owner.
// OnDisable deregisters only; pending enable/auto-select callbacks are not canceled.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetNavigation : MonoBehaviour
    {
        [SerializeField] private bool m_autoSelect;
        [SerializeField] private bool m_showHighlight = true;
        [SerializeField, ShowIf("m_showHighlight")] private UIHighlightDefinition m_highlight;
        [SerializeField] private bool m_highlightUseSpawnPoint;
        [ShowIf("m_highlightUseSpawnPoint"), SerializeField] private RectTransform m_highlightSpawnPoint;
        [SerializeField] private UITransitionAnimation m_transitionAnimation;
        [SerializeField] private bool m_allowVerticalNavigation = true;
        [SerializeField] private bool m_allowHorizontalNavigation = true;
        [SerializeField] private bool m_changeAnimationState = true;
        [SerializeField] private UnityEvent m_onSelected;
        [SerializeField] private UnityEvent m_onDeSelected;

        public bool ShowHighlight { get { return m_showHighlight; } }
        public UIHighlightDefinition Highlighter { get { return m_highlight; } }
        public bool AllowVerticalNavigation { get { return m_allowVerticalNavigation; } }
        public bool AllowHorizontalNavigation { get { return m_allowHorizontalNavigation; } }
        public bool AutoSelect { get { return m_autoSelect; } set { m_autoSelect = value; } }
        public bool IsSelected { get; private set; }
        public event Action<UIWidgetNavigation> OnWidgetSelected = _ => { };

        private UINavigationManager m_uiNavigationManager;
        private bool m_hasTransitionAnimation;

        private void Start() { m_hasTransitionAnimation = m_transitionAnimation != null; }

        private void OnEnable()
        {
            ProcessManager.GetSystemRef<UINavigationManager>().InvokeOnValid(OnNavigationManagerValid);
        }

        private void OnDisable() { m_uiNavigationManager?.DeregisterWidget(this); }

        private void OnNavigationManagerValid(UINavigationManager uiNavigationManager)
        {
            m_uiNavigationManager = uiNavigationManager;
            m_uiNavigationManager.RegisterWidget(this);
            if (m_autoSelect)
                StartCoroutine(this.WaitForUI(() => m_uiNavigationManager.Select(this)));
        }

        public void OnSelect()
        {
            IsSelected = true;
            if (m_hasTransitionAnimation && m_changeAnimationState)
                m_transitionAnimation.QueueTransitionToState(UITransitionState.Selected);
            OnWidgetSelected(this);
            m_onSelected?.Invoke();
        }

        public void OnDeselect()
        {
            if (m_hasTransitionAnimation && m_changeAnimationState)
                m_transitionAnimation.QueueTransitionToState(UITransitionState.Normal);
            m_onDeSelected?.Invoke();
            IsSelected = false;
        }

        public Transform GetHighlightParent()
        {
            return m_highlightUseSpawnPoint ? m_highlightSpawnPoint : transform;
        }

        public UIWidgetNavigation() { }
    }
}
