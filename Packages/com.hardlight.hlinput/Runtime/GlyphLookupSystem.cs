using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GlyphLookupSystem : IGlyphLookupSystemSetter
    {
        private Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> m_gameInputDictionary =
            new Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>>(HardlightEnumComparers.GameInputComparer);

        // Original HLInput.Runtime 0x06000269. An empty receiver adopts the exact caller map.
        // Later merges retain old entries and use live indexers rather than pair.Value snapshots;
        // mutation and null faults may therefore leave a partially updated, aliased dictionary.
        void IGlyphLookupSystemSetter.SetGameInputGlyphMap(
            Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> generatedMap)
        {
            if (m_gameInputDictionary.Count == 0)
            {
                m_gameInputDictionary = generatedMap;
                return;
            }

            foreach (KeyValuePair<GameInput, Dictionary<InputType, List<Texture2D>>> inputPair in generatedMap)
            {
                Dictionary<InputType, List<Texture2D>> inputDictionary;
                if (!m_gameInputDictionary.TryGetValue(inputPair.Key, out inputDictionary))
                {
                    inputDictionary = new Dictionary<InputType, List<Texture2D>>(HardlightInputEnumComparers.InputTypeComparer);
                    m_gameInputDictionary.Add(inputPair.Key, inputDictionary);
                }

                foreach (KeyValuePair<InputType, List<Texture2D>> typePair in generatedMap[inputPair.Key])
                {
                    List<Texture2D> glyphs;
                    if (!m_gameInputDictionary[inputPair.Key].TryGetValue(typePair.Key, out glyphs))
                    {
                        glyphs = new List<Texture2D>();
                        m_gameInputDictionary[inputPair.Key].Add(typePair.Key, glyphs);
                    }

                    foreach (Texture2D glyph in generatedMap[inputPair.Key][typePair.Key])
                        glyphs.AddUnique(glyph);
                }
            }
        }

        // Original 0x0600026a. Empty lists and missing keys retain indexer faults.
        public Texture2D GetGlyphForGameInput(GameInput input, InputType inputType)
        {
            return m_gameInputDictionary[input][inputType][0];
        }

        // Original 0x0600026b. The out value is assigned before Unity's null comparison;
        // a destroyed first glyph remains in the out slot even when this returns false.
        public bool TryGetGlyphForGameInput(GameInput input, InputType inputType, out Texture2D glyph)
        {
            glyph = null;
            Dictionary<InputType, List<Texture2D>> inputDictionary;
            List<Texture2D> glyphs;
            if (m_gameInputDictionary != null && m_gameInputDictionary.TryGetValue(input, out inputDictionary)
                && inputDictionary != null && inputDictionary.TryGetValue(inputType, out glyphs)
                && glyphs != null && glyphs.Count != 0)
            {
                glyph = glyphs[0];
                return glyph != null;
            }
            return false;
        }

        // Original 0x0600026c. Only key existence is tested; values/list length are not guarded.
        public Texture2D Debug_GetGlyphForGameInput(GameInput input, InputType inputType)
        {
            if (m_gameInputDictionary.ContainsKey(input) && m_gameInputDictionary[input].ContainsKey(inputType))
                return GetGlyphForGameInput(input, inputType);
            return null;
        }

        // Original 0x0600026d is the implicit public constructor: the field initializer runs
        // before Object's constructor, using the genuine generated GameInput comparer.
    }
}
