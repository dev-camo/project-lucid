// Original HLUnityUI.Runtime Hardlight.HLVerticalRecyclingScrollRect`2 020000fb,
// reconstructed from the fully shared x86-64 bodies (0x1394A40..0x1394C20) and metadata.
// No original type derives from it; the exported script has no asset references.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class HLVerticalRecyclingScrollRect<T1, T2> : HLRecyclingScrollRect<T1, T2> where T1 : HLRecyclingScrollRectElement<T2>
    {
        [SerializeField]
        protected HorizontalAlignment m_childAlignment = HorizontalAlignment.Center;

        protected override float StartPadding => m_internalPadding.top;
        protected override float EndPadding => m_internalPadding.bottom;

        // The scroll rect's content also holds both external paddings.
        protected override float ContentSize
        {
            get => m_content.rect.height;
            set
            {
                m_scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, m_externalPadding.x + m_externalPadding.y + value);
                m_content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, value);
            }
        }

        protected override float ViewportSize => m_scrollRect.viewport.rect.height;
        protected override float ElementSize => m_listEntryPrefab.RectTransform.rect.height;

        protected override float GetNormalizedPositionOnAxis(Vector2 normalizedPosition)
        {
            return normalizedPosition.y;
        }

        // 0x06000706: positions run down from the top, so y is the negated position; a
        // right child sits inside the right padding, and an undefined alignment leaves
        // the child where it is.
        protected override void SetChildPosition(ref T1 child, float position)
        {
            switch (m_childAlignment)
            {
                case HorizontalAlignment.Left:
                    child.RectTransform.anchoredPosition = new Vector2(m_internalPadding.left, -position);
                    break;
                case HorizontalAlignment.Center:
                    child.RectTransform.anchoredPosition = new Vector2(0f, -position);
                    break;
                case HorizontalAlignment.Right:
                    child.RectTransform.anchoredPosition = new Vector2(-m_internalPadding.right, -position);
                    break;
            }
        }
    }
}
