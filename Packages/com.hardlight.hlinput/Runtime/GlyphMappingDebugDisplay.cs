using UnityEngine;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GlyphMappingDebugDisplay : MonoBehaviour
    {
        [SerializeField] private Text text;
        [SerializeField] private RawImage rawImage;
        public void SetupDisplay(string gameInputText, Texture2D texture) // 060000c8
        {
            text.text = gameInputText;
            rawImage.texture = texture;
        }
        public GlyphMappingDebugDisplay() { } // 060000c9
    }
}
