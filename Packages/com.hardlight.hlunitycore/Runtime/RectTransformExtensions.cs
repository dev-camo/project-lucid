using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    public static class RectTransformExtensions
    {
        // Original HLUnityCore.Runtime 06000300. Clear precedes receiver access;
        // childCount is read on every iteration and non-RectTransform children
        // are retained as null entries. No active-state or Unity-null filter.
        public static void GetImmediateChildren(this RectTransform rectTransform, List<RectTransform> children)
        {
            children.Clear();
            for (int i = 0; i < rectTransform.childCount; ++i)
                children.Add(rectTransform.GetChild(i) as RectTransform);
        }

        // Original 06000301: anchorMin, anchorMax, offsetMin, offsetMax in order.
        // Shipping native bodies load Vector3.zero/one and use their first two
        // components; the implicit Vector3-to-Vector2 conversion is preserved.
        public static void ResetToFullscreen(this RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector3.zero;
            rectTransform.anchorMax = Vector3.one;
            rectTransform.offsetMin = Vector3.zero;
            rectTransform.offsetMax = Vector3.zero;
        }

        // Original 06000302. Preserve Unity's approximate Vector2 inequality,
        // max-axis division, one rect read and local-space return. Direction
        // is multiplied by 0.5 before rect size; do not reassociate the products.
        public static Vector3 GetPointOnRectEdge(this RectTransform rectTransform, Vector2 localDirection)
        {
            if (localDirection != Vector2.zero)
                localDirection /= Mathf.Max(Mathf.Abs(localDirection.x), Mathf.Abs(localDirection.y));
            Rect rect = rectTransform.rect;
            return rect.center + Vector2.Scale(localDirection * 0.5f, rect.size);
        }
    }
}
