// Original HLModernUI.Runtime.dll owner 0x02000013.
// 0x06000039 returns m_prefab unchanged; 0x0600003a calls the genuine
// ScriptableObjectWithGuid constructor, without a new local field initializer.
// Original attribute order, serialized field name and sealed identity are retained.
// Inherited Guid/provider algorithms and Unity serialization remain separate obligations.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "UIHighlightDefinition", menuName = "Hardlight/HLModernUI/UIHighlightDefinition", order = 0)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIHighlightDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private UIHighlight m_prefab;

        public UIHighlight Prefab => m_prefab;

        public UIHighlightDefinition() { }
    }
}
