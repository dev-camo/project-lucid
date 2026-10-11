// Preservation reconstruction of complete original Hardlight.UINavigationConfiguration in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using Hardlight.Utils;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "UINavigationConfiguration", menuName = "Hardlight/HLModernUI/UINavigationConfiguration", order = 0)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UINavigationConfiguration : SystemConfigurationAsset
    {
        [Header("Selection"), HashEnum(typeof(GameInput)), SerializeField] private GameInput m_selectionLeftInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_selectionRightInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_selectionUpInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_selectionDownInput;
        [SerializeField, Header("Highlights")] private UIHighlightDefinition m_defaultHighlightPrefab;

        public GameInput SelectionLeftInput => m_selectionLeftInput;
        public GameInput SelectionRightInput => m_selectionRightInput;
        public GameInput SelectionUpInput => m_selectionUpInput;
        public GameInput SelectionDownInput => m_selectionDownInput;
        public UIHighlightDefinition DefaultHighlightPrefab => m_defaultHighlightPrefab;
        public override void Validate() { }
        public UINavigationConfiguration() { }
    }
}
