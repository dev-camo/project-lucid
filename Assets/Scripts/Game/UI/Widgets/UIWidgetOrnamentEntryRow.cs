using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009b8; bodies translated from the 1.10.1 x86-64 binary.
    // One row of ornament thumbnails; the toggle count follows the row data.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetOrnamentEntryRow : UIWidget
    {
        [SerializeField] private UIWidgetOrnamentToggle m_thumbnailPrefab;
        private readonly SystemRef<UINavigationManager> m_uiNavigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>(null, true);
        private readonly List<UIWidgetOrnamentToggle> m_entriesInThisRow = new List<UIWidgetOrnamentToggle>();

        // 0x0600380f (0x69E350): grows then shrinks the toggle list to the data count, then sets
        // up each toggle with its parameters.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            UIWidgetOrnamentEntryRowParameters data = parameters.GetAs<UIWidgetOrnamentEntryRowParameters>();
            while (data.Toggles.Count > m_entriesInThisRow.Count)
                m_entriesInThisRow.Add(Instantiate(m_thumbnailPrefab, transform));
            while (m_entriesInThisRow.Count > data.Toggles.Count)
            {
                Destroy(m_entriesInThisRow[m_entriesInThisRow.Count - 1].gameObject);
                m_entriesInThisRow.RemoveAt(m_entriesInThisRow.Count - 1);
            }
            for (int i = 0; i < m_entriesInThisRow.Count; i++)
            {
                UIWidgetOrnamentToggleParameters toggleData = data.Toggles[i];
                m_entriesInThisRow[i].Setup(toggleData);
            }
        }

        // 0x06003810 (0x69E6F0)
        public void SelectFirst()
        {
            if (m_uiNavigationManagerRef.TryGet(out UINavigationManager navigationManager))
                navigationManager.Select(m_entriesInThisRow[0].WidgetNavigation);
        }

        // 0x06003811 (0x69E780)
        public UIWidgetOrnamentEntryRow() { }
    }
}
