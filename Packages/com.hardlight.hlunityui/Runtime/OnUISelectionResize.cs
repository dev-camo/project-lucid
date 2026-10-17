// Original HLUnityUI.Runtime Hardlight.OnUISelectionResize 02000095, from the x86-64
// bodies 0x1B5F700..0x1B5F850 and metadata.
using UnityEngine;

namespace Hardlight
{
    public class OnUISelectionResize : ModifiedOnUISelection
    {
        [SerializeField]
        private RectTransform m_rectTransform;
        private Vector2 m_reusableVector2;

        private void Reset()
        {
            m_rectTransform = transform as RectTransform;
        }

        // 0x0600048b: the layer's Left/Bottom inset becomes offsetMin and its Right/Top inset
        // becomes the negated offsetMax; the field keeps the un-negated Right/Top pair.
        public override void OnHighlightUpdated(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource, float lerpValue)
        {
            m_reusableVector2.x = Mathf.Lerp(previousSource.Left, currentSource.Left, lerpValue);
            m_reusableVector2.y = Mathf.Lerp(previousSource.Bottom, currentSource.Bottom, lerpValue);
            m_rectTransform.offsetMin = m_reusableVector2;

            m_reusableVector2.x = Mathf.Lerp(previousSource.Right, currentSource.Right, lerpValue);
            m_reusableVector2.y = Mathf.Lerp(previousSource.Top, currentSource.Top, lerpValue);
            m_rectTransform.offsetMax = -m_reusableVector2;
        }
    }
}
