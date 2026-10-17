// Original HLUnityUI.Runtime Hardlight.OnUISelectionSwapColour 02000097, from the x86-64
// bodies 0x1B5F910..0x1B5FA70 and metadata.
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
    [RequireComponent(typeof(Image))]
    public class OnUISelectionSwapColour : ModifiedOnUISelection
    {
        [SerializeField]
        private Image m_image;
        private Color m_currentColourBlend;

        private void Reset()
        {
            m_image = GetComponent<Image>();
        }

        // 0x06000491: the colour is blended through LAB space, which does not carry alpha, so
        // the image colour is read back and its alpha is lerped separately in RGB space.
        public override void OnHighlightUpdated(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource, float lerpValue)
        {
            m_image.color = LABColor.Lerp(previousSource.Color, currentSource.Color, lerpValue);
            m_currentColourBlend = m_image.color;
            m_currentColourBlend.a = Mathf.Lerp(previousSource.Color.a, currentSource.Color.a, lerpValue);
            m_image.color = m_currentColourBlend;
        }
    }
}
