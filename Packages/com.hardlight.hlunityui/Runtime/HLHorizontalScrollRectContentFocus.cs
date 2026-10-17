using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityUI.Runtime 020000c8 (MethodDefs 0x06000568..56b): content
    // focus along x, sized by each child's rect width.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLHorizontalScrollRectContentFocus : HLScrollRectContentFocus
    {
        // 0x06000568 @0x1B66120.
        public override void Refocus()
        {
            m_contentRelativeOffset = new Vector2(m_accumulatedPositionDelta, 0f);
            base.Refocus();
        }

        // 0x06000569 @0x1B66140.
        protected override void CalculatePositionDelta(RectTransform child, Interaction interactionType, int index = 0)
        {
            InternalCalculatePositionDelta(RectTransform.Axis.Horizontal, child.rect.width, interactionType, index);
        }

        // 0x0600056a @0x1B66180. Once an add or remove has armed m_preserveVelocity,
        // the previous x velocity is restored once, and only when Mathf.Sign of the
        // current one is +1 while the previous sign differed and its magnitude
        // exceeds float.Epsilon. The velocity is sampled every frame.
        private new void LateUpdate()
        {
            base.LateUpdate();
            if (m_preserveVelocity)
            {
                float currentSign = Mathf.Sign(m_scrollRect.velocity.x);
                if (currentSign > float.Epsilon
                    && Math.Abs(Mathf.Sign(m_previousScrollRectVelocity.x) - currentSign) > float.Epsilon
                    && Math.Abs(m_previousScrollRectVelocity.x) > float.Epsilon)
                {
                    m_scrollRect.velocity = m_previousScrollRectVelocity;
                    m_preserveVelocity = false;
                }
            }
            m_previousScrollRectVelocity = m_scrollRect.velocity;
        }

        // 0x0600056b @0x1B662C0.
        public HLHorizontalScrollRectContentFocus() { }
    }
}
