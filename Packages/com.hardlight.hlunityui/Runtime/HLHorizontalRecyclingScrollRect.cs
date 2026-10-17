// Original HLUnityUI.Runtime Hardlight.HLHorizontalRecyclingScrollRect`2 020000e5,
// reconstructed from the fully shared x86-64 bodies (0x1392F80..0x1393150) and metadata.
// No original type derives from it; the exported script has no asset references.
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class HLHorizontalRecyclingScrollRect<T1, T2> : HLRecyclingScrollRect<T1, T2> where T1 : HLRecyclingScrollRectElement<T2>
    {
        [SerializeField]
        protected VerticalAlignment m_childAlignment = VerticalAlignment.Center;

        protected override float StartPadding => m_internalPadding.left;
        protected override float EndPadding => m_internalPadding.right;

        // The scroll rect's content also holds both external paddings.
        protected override float ContentSize
        {
            get => m_content.rect.width;
            set
            {
                m_scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_externalPadding.x + m_externalPadding.y + value);
                m_content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value);
            }
        }

        protected override float ViewportSize => m_scrollRect.viewport.rect.width;
        protected override float ElementSize => m_listEntryPrefab.RectTransform.rect.width;

        protected override float GetNormalizedPositionOnAxis(Vector2 normalizedPosition)
        {
            return normalizedPosition.x;
        }

        // 0x06000645: a top child sits below the top padding, a bottom child above the
        // bottom padding; an undefined alignment leaves the child where it is.
        protected override void SetChildPosition(ref T1 child, float position)
        {
            switch (m_childAlignment)
            {
                case VerticalAlignment.Top:
                    child.RectTransform.anchoredPosition = new Vector2(position, -m_internalPadding.top);
                    break;
                case VerticalAlignment.Center:
                    child.RectTransform.anchoredPosition = new Vector2(position, 0f);
                    break;
                case VerticalAlignment.Bottom:
                    child.RectTransform.anchoredPosition = new Vector2(position, m_internalPadding.bottom);
                    break;
            }
        }
    }
}
