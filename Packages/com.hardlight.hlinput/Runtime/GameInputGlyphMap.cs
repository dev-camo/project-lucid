using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create GameInputGlyphMap")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameInputGlyphMap : ScriptableObject
    {
        [SerializeField] private List<BaseGlyphMap<GameInput>.KeyGlyphMap> m_populateDataFromGameInput;
        [SerializeField] private InputType m_inputType;
        private readonly Dictionary<GameInput, Texture2D> m_cachedLookup =
            new Dictionary<GameInput, Texture2D>(HardlightEnumComparers.GameInputComparer);
        public InputType InputType => m_inputType; // Original06000263.
        private void OnValidate() { Initialise(); } // Original06000264.
        public void Initialise() // Original06000265 preserves first duplicate, present null, live iteration and partial faults.
        {
            m_cachedLookup.Clear();
            foreach (BaseGlyphMap<GameInput>.KeyGlyphMap glyphMap in m_populateDataFromGameInput)
                if (!m_cachedLookup.ContainsKey(glyphMap.Key)) m_cachedLookup[glyphMap.Key] = glyphMap.Glyph;
        }
        public void Shutdown() { m_cachedLookup.Clear(); } // Original06000266.
        public Texture2D GetGlyphForGameInput(GameInput gameInput) // Original06000267 ignores TryGetValue boolean.
        {
            m_cachedLookup.TryGetValue(gameInput, out Texture2D glyph);
            return glyph;
        }
        public GameInputGlyphMap() : base() { } // Original06000268 cache constructed before real Unity base.
    }
}
