using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original Assembly-CSharp-firstpass; bodies translated from the 1.10.1 x86-64 binary.
    // Remembers the selected widget of a container through the UIManager and reselects it
    // when the container is created or when only the listed containers remain open.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(UIContainer))]
    public class UIWidgetRetainLastSelected : MonoBehaviour
    {
        [SerializeField] private UIContainer m_uiContainer;
        [SerializeField] private UIContainerNavigation m_uiContainerNavigation;
        [Tooltip("Reselect when only these UI Containers are open, so will trigger when other containers close")]
        [SerializeField] private UIContainerIdentifier[] m_selectWhenExclusivelyOpen = System.Array.Empty<UIContainerIdentifier>();
        private UIManager m_uiManager;
        private UINavigationManager m_uiNavigationManager;

        // 06000003
        private void Reset()
        {
            m_uiContainer = GetComponent<UIContainer>();
        }

        // 06000004
        private void Awake()
        {
            m_uiManager = ProcessManager.GetSystem<UIManager>();
            m_uiNavigationManager = ProcessManager.GetSystem<UINavigationManager>();
            m_uiManager.OnContainerClosed += OnUIContainerClosed;
            RetrieveLastSelected();
        }

        // 06000005
        private void OnDestroy()
        {
            if (ProcessManager.GetSystemRef<UIManager>().IsValid())
                m_uiManager.OnContainerClosed -= OnUIContainerClosed;
        }

        // 06000006
        private void OnUIContainerClosed(UIContainerIdentifier identifier)
        {
            if (m_uiManager.AreExclusivelyOpen(m_selectWhenExclusivelyOpen))
                RetrieveLastSelected();
        }

        // 06000007. Every widget is observed again on each call; OnDisable removes one subscription.
        private void RetrieveLastSelected()
        {
            bool hasSelection = m_uiManager.TryGetSelectedWidget(m_uiContainer, out int selectedIndex);
            for (int i = 0; i < m_uiContainerNavigation.NavigableWidgets.Length; i++)
            {
                UIWidgetNavigation navigableWidget = m_uiContainerNavigation.NavigableWidgets[i];
                navigableWidget.OnWidgetSelected += OnSelected;
                if (!hasSelection)
                    continue;
                navigableWidget.AutoSelect = i == selectedIndex;
                if (i == selectedIndex)
                    StartCoroutine(this.WaitForUI(() => m_uiNavigationManager.Select(navigableWidget)));
            }
        }

        // 06000008
        private void OnDisable()
        {
            foreach (UIWidgetNavigation navigableWidget in m_uiContainerNavigation.NavigableWidgets)
                navigableWidget.OnWidgetSelected -= OnSelected;
        }

        // 06000009
        private void OnSelected(UIWidgetNavigation widget)
        {
            int index = m_uiContainerNavigation.NavigableWidgets.FindIndex(navigableWidget => navigableWidget == widget);
            if (index != -1)
                m_uiManager.SetSelectedWidget(m_uiContainer, index);
        }
    }
}
