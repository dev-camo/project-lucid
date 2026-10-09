using UnityEngine;

namespace Hardlight
{
    // Original HLInput.Runtime 02000092: complete fieldless generic interface.
    // TKey is invariant, unconstrained; this owner has no parent interfaces.
    public interface IBaseInputGlyphSource<TKey>
    {
        // Original 06000295: genuine abstract slot0; texture is an out parameter.
        // No native implementation exists on this contract owner.
        bool TryGetGlyph(TKey key, int joystickIndex, out Texture2D texture);
    }
}
