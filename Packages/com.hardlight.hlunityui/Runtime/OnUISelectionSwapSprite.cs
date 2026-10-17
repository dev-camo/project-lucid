// Original HLUnityUI.Runtime Hardlight.OnUISelectionSwapSprite 02000098, from the x86-64
// bodies 0x1B5FA70..0x1B5FBA0 and metadata.
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
    [RequireComponent(typeof(Image))]
    public class OnUISelectionSwapSprite : ModifiedOnUISelection
    {
        [SerializeField]
        private Image m_image;
        private bool m_spritesSwapped;

        private void Reset()
        {
            m_image = GetComponent<Image>();
        }

        public override void OnActivate(HLButtonHighlightCustomisations.ButtonHighlightChangeSource source)
        {
            m_image.sprite = source.Sprite;
            m_image.material = source.Material;
        }

        // 0x06000495: swaps once per highlight, from the halfway point on. The guard at
        // 0x1B5FB1F is `0.5 > lerpValue`, so a NaN lerp value also swaps.
        public override void OnHighlightUpdated(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource, float lerpValue)
        {
            if (lerpValue < 0.5f || m_spritesSwapped)
                return;

            m_image.sprite = currentSource.Sprite;
            m_image.material = currentSource.Material;
            m_spritesSwapped = true;
        }

        // 0x06000496: a highlight between the same source instance has nothing to swap, so it
        // starts as already swapped. The test is reference equality.
        public override void OnHighlightBegin(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource)
        {
            m_spritesSwapped = previousSource == currentSource;
        }
    }
}
