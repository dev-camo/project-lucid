// Original HLUnityUI.Runtime Hardlight.OnUISelectionSkew 02000096, from the x86-64
// bodies 0x1B5F850..0x1B5F910 and metadata.
using UnityEngine;

namespace Hardlight
{
    [RequireComponent(typeof(Skew))]
    public class OnUISelectionSkew : ModifiedOnUISelection
    {
        [SerializeField]
        private Skew m_skew;
        private Vector2 m_skewAmount;

        private void Reset()
        {
            m_skew = GetComponent<Skew>();
        }

        public override void OnHighlightUpdated(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource, float lerpValue)
        {
            m_skewAmount = Vector2.Lerp(new Vector2(previousSource.SkewX, previousSource.SkewY), new Vector2(currentSource.SkewX, currentSource.SkewY), lerpValue);
            m_skew.ChangeSkewAmount(m_skewAmount.x, m_skewAmount.y, true);
        }
    }
}
