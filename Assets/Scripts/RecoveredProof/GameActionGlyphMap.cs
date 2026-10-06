using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "GameActionGlyphMap", menuName = "HardlightProject/DefinitionData/Definitions/GameActionGlyphMap")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameActionGlyphMap : ScriptableObject
    {
        [SerializeField] private List<BaseGlyphMap<GameAction>.KeyGlyphMap> m_actionToGlyphMap;
        [SerializeField] private InputType m_inputType;
        private readonly Dictionary<GameAction, Texture2D> m_cachedLookup = new Dictionary<GameAction, Texture2D>();

        // Game.Runtime 0x06001324/0x06001325: direct field getter, then the
        // original validation hook routes to the same initialization method.
        public InputType InputType { get { return m_inputType; } }
        private void OnValidate() { Initialise(); }

        // Game.Runtime 0x06001326: clears before obtaining the list enumerator,
        // first duplicate wins, and insertion uses the dictionary indexer.
        public void Initialise()
        {
            m_cachedLookup.Clear();
            foreach (BaseGlyphMap<GameAction>.KeyGlyphMap keyGlyphMap in m_actionToGlyphMap)
                if (!m_cachedLookup.ContainsKey(keyGlyphMap.Key))
                    m_cachedLookup[keyGlyphMap.Key] = keyGlyphMap.Glyph;
        }

        // Game.Runtime 0x06001327/0x06001328: direct Clear/TryGetValue routes.
        public void Shutdown() { m_cachedLookup.Clear(); }
        public bool TryGetGlyph(GameAction action, out Texture2D glyph) { return m_cachedLookup.TryGetValue(action, out glyph); }
        // Original 0x06001329 initializes only m_cachedLookup before the
        // ScriptableObject base constructor; authored lists remain null.
    }
}
