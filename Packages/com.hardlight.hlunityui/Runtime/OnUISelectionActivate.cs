// Original HLUnityUI.Runtime Hardlight.OnUISelectionActivate 02000094, from the x86-64
// bodies 0x1B5F680..0x1B5F700 and metadata. OnActivate is not overridden
// (dump.cs 11972 lists only slots 5 and 8).
using UnityEngine;

namespace Hardlight
{
    public class OnUISelectionActivate : ModifiedOnUISelection
    {
        // 0x06000487: follows the current layer's Enabled flag on every update; the lerp
        // value and the previous source are never read.
        public override void OnHighlightUpdated(HLButtonHighlightCustomisations.ButtonHighlightChangeSource previousSource, HLButtonHighlightCustomisations.ButtonHighlightChangeSource currentSource, float lerpValue)
        {
            gameObject.SetActive(currentSource.Enabled);
        }

        public override void OnDeactivate(HLButtonHighlightCustomisations.ButtonHighlightChangeSource source)
        {
            gameObject.SetActive(false);
        }
    }
}
