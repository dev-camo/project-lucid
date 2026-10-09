using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create KeyCodeGlyphMap")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class KeyCodeGlyphMap : BaseGlyphMap<KeyCode>
    {
        [Tooltip("Populate with GameInputBindings to then dynamically fill used KeyCode values.")]
        [SerializeField] private List<GameInputBinding> m_populateDataFromBindings = new List<GameInputBinding>();
        public KeyCodeGlyphMap() : base() { } // Original0600026f allocates authored list before genuine base constructor.
    }
}
