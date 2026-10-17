using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 020000d6 (MethodDefs 0x060005c9..5cc): content
    // focus along y, sized by each child's rect height.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLVerticalScrollRectContentFocus : HLScrollRectContentFocus
    {
        // 0x060005c9 @0x1B6B0A0.
        public override void Refocus()
        {
            m_contentRelativeOffset = new Vector2(0f, m_accumulatedPositionDelta);
            base.Refocus();
        }

        // 0x060005ca @0x1B6B0C0 (the base calculation is inlined in retail).
        protected override void CalculatePositionDelta(RectTransform child, Interaction interactionType, int index = 0)
        {
            InternalCalculatePositionDelta(RectTransform.Axis.Vertical, child.rect.height, interactionType, index);
        }

        // 0x060005cb @0x1B6B170: the horizontal rule applied to the y velocity.
        private new void LateUpdate()
        {
            base.LateUpdate();
            if (m_preserveVelocity)
            {
                float currentSign = Mathf.Sign(m_scrollRect.velocity.y);
                if (currentSign > float.Epsilon
                    && Math.Abs(Mathf.Sign(m_previousScrollRectVelocity.y) - currentSign) > float.Epsilon
                    && Math.Abs(m_previousScrollRectVelocity.y) > float.Epsilon)
                {
                    m_scrollRect.velocity = m_previousScrollRectVelocity;
                    m_preserveVelocity = false;
                }
            }
            m_previousScrollRectVelocity = m_scrollRect.velocity;
        }

        // 0x060005cc @0x1B6B2C0.
        public HLVerticalScrollRectContentFocus() { }
    }
}
