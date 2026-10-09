using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000066: all three owner methods. The real
    // generated <>c constructor, initializer and dictionary factory are retained
    // as native evidence; compiler-emitted closure naming remains unbound.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputOnScreen
    {
        public virtual void Initialise(
            ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<GameInput, Texture2D> getGlyphForGameInput, InputType inputType) // 0x060001ff
        {
            PopulateToGlyphMap(ref gameInputGlyphMap, getGlyphForGameInput, inputType);
        }

        // 0x06000200: callback precedes Unity object equality and all dictionary
        // operations. Existing glyph lists accumulate entries across calls.
        private void PopulateToGlyphMap(
            ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<GameInput, Texture2D> getGlyphForGameInput, InputType inputType)
        {
            foreach (GameInput gameInput in EnumUtilities.GetValues<GameInput>())
            {
                Texture2D glyph = getGlyphForGameInput(gameInput);
                if (glyph == null)
                    continue;
                gameInputGlyphMap.TryGetOrNew(gameInput,
                    () => new Dictionary<InputType, List<Texture2D>>(HardlightInputEnumComparers.InputTypeComparer))
                    .TryGetOrNew(inputType).Add(glyph);
            }
        }

        public InputOnScreen() { } // 0x06000201: System.Object only.
    }
}
