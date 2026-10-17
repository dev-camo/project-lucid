using UnityEngine.UI;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000080 (06000305), body from the 1.10.1
    // x86-64 binary.
    public static class ScrollRectExtensions
    {
        // 0x06000305 (0x1ab39a0): stop, then snap each enabled axis to its start;
        // the vertical start is 1 (constant 0x3f800000 at 0x29a6be0).
        public static void ResetScroll(this ScrollRect scrollRect)
        {
            if (scrollRect != null)
            {
                scrollRect.StopMovement();
                if (scrollRect.horizontal)
                {
                    scrollRect.horizontalNormalizedPosition = 0f;
                }
                if (scrollRect.vertical)
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }
    }
}
