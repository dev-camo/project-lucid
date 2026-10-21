using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ba; bodies translated from the 1.10.1 x86-64 binary.
    // Spawns one ornament entry row per row of data under this transform.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetOrnamentEntryRowComposer : UIWidget
    {
        [SerializeField] private UIWidgetOrnamentEntryRow m_rowPrefab;

        // 0x06003813 (0x69E8E0): the first row selects its first toggle. The native body inlines
        // SelectFirst (navigation manager lookup, then Select of entry zero).
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            UIWidgetOrnamentEntryRowComposerParameters data = parameters.GetAs<UIWidgetOrnamentEntryRowComposerParameters>();
            for (int i = 0; i < data.RowData.Count; i++)
            {
                UIWidgetOrnamentEntryRowParameters rowData = data.RowData[i];
                UIWidgetOrnamentEntryRow row = Instantiate(m_rowPrefab, transform);
                row.Setup(rowData);
                if (i == 0)
                    row.SelectFirst();
            }
        }

        // 0x06003814 (0x69EB40): destroys every child.
        public void Clear()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
        }

        // 0x06003815 (0x69EE20)
        public UIWidgetOrnamentEntryRowComposer() { }
    }
}
