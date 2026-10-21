using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009bb; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetOrnamentEntryRowComposerParameters : IUIWidgetParameters
    {
        public readonly IReadOnlyList<UIWidgetOrnamentEntryRowParameters> RowData;

        // 0x06003816 (0x69EE30)
        public UIWidgetOrnamentEntryRowComposerParameters(IReadOnlyList<UIWidgetOrnamentEntryRowParameters> rowData)
        {
            RowData = rowData;
        }
    }
}
