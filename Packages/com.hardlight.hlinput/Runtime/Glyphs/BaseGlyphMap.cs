using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BaseGlyphMap<TKey> : ScriptableObject
    {
        [Tooltip("Glyph to show when an input, or a key is missing.")]
        [SerializeField] private Texture2D m_missingGlyph;
        [SerializeField] protected List<MappedGlyphs> m_inputGlyphs;
        private readonly Dictionary<InputType, Dictionary<TKey, Texture2D>> m_inputTypeDictionary =
            new Dictionary<InputType, Dictionary<TKey, Texture2D>>(HardlightInputEnumComparers.InputTypeComparer);

        // HLInput.Runtime 0x06000256: initialization adds directly to retained
        // dictionaries. Duplicate input types or keys throw after earlier adds.
        public void Initialise()
        {
            foreach (MappedGlyphs mappedGlyphs in m_inputGlyphs)
            {
                m_inputTypeDictionary.Add(mappedGlyphs.InputType, new Dictionary<TKey, Texture2D>());
                foreach (KeyGlyphMap keyGlyphMap in mappedGlyphs.GlyphMaps)
                    m_inputTypeDictionary[mappedGlyphs.InputType].Add(keyGlyphMap.Key, keyGlyphMap.Glyph);
            }
        }

        // HLInput.Runtime 0x06000257; preserves authored lists and fallback glyph.
        public void Shutdown() { m_inputTypeDictionary.Clear(); }

        // HLInput.Runtime 0x06000258: a present null glyph is returned as null;
        // the fallback is used only when the input type or key is absent.
        public Texture2D GetGlyphForKeyAndInputType(TKey key, InputType inputType)
        {
            if (m_inputTypeDictionary.TryGetValue(inputType, out Dictionary<TKey, Texture2D> glyphs))
                return glyphs.TryGetValue(key, out Texture2D glyph) ? glyph : m_missingGlyph;
            return m_missingGlyph;
        }

        // HLInput.Runtime 0x06000259/0x0600025a: dictionary constructor uses the
        // original input comparer; ScriptableObject base follows field setup.
        public bool ContainsGlyphsForInput(InputType inputType) { return m_inputTypeDictionary.ContainsKey(inputType); }

        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class KeyGlyphMap : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            public TKey Key;
            public Texture2D Glyph;
            // HLInput.Runtime 0x0600025b/0x0600025c: both callbacks overwrite
            // the display name from the key; they do not parse Name into Key.
            public void OnBeforeSerialize() { Name = Key.ToString(); }
            public void OnAfterDeserialize() { Name = Key.ToString(); }
            // Original 0x0600025d is the natural Object base-only constructor.
        }

        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class MappedGlyphs : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            public InputType InputType;
            public List<KeyGlyphMap> GlyphMaps;
            // HLInput.Runtime 0x0600025e/0x0600025f; neither callback initializes
            // or visits the glyph list. Original 0x06000260 is base-only.
            public void OnBeforeSerialize() { Name = InputType.ToString(); }
            public void OnAfterDeserialize() { Name = InputType.ToString(); }
        }
    }
}
