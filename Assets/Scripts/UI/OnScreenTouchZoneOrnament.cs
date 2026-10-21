using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200097e; bodies translated from the 1.10.1 x86-64 binary.
    // A touch zone that reports each horizontal drag step as a fraction of the zone's width and keeps the
    // last step as an impulse once the finger lifts.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OnScreenTouchZoneOrnament : OnScreenTouchZone
    {
        private float m_minX;
        private float m_maxX;
        private float m_touchSpan;
        private float m_dragDelta;
        private float m_dragReleaseImpulse;
        private float m_dragAmount = float.NaN;

        // 060036e2 (1.10.1 RVA 0x692F10): NaN while nothing is being dragged.
        public float DragAmount => m_dragAmount;

        // 060036e3 (RVA 0x692F20): the world-space x extent of the valid touch rectangle (corners 0 and 2).
        public override void Initialise()
        {
            base.Initialise();
            Vector3[] corners = new Vector3[4];
            m_validTouchTransform.GetWorldCorners(corners);
            m_minX = corners[0].x;
            m_maxX = corners[2].x;
            m_touchSpan = m_maxX - m_minX;
        }

        // 060036e4 (RVA 0x692FD0): the ornament zone supplies no game input.
        protected override void SupplyHeldGameInput(Vector2 resultantMovement)
        {
        }

        // 060036e5 (RVA 0x692FE0)
        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta)
        {
            m_dragDelta = touchDelta.x;
            m_dragAmount = touchDelta.x / m_touchSpan;
            return new Vector2(m_dragAmount, 0f);
        }

        // 060036e6 (RVA 0x693010)
        protected override void OnTouchRelease()
        {
            m_dragReleaseImpulse = m_dragDelta;
            m_dragAmount = float.NaN;
        }

        // 060036e7 (RVA 0x693030): zero is returned untouched; any other value, NaN included, is cleared.
        public float ConsumeDragReleaseImpulse()
        {
            float impulse = m_dragReleaseImpulse;
            if (impulse == 0f) return 0f;
            m_dragReleaseImpulse = 0f;
            return impulse;
        }

        // 060036e8 (RVA 0x693060)
        public OnScreenTouchZoneOrnament()
        {
        }
    }
}
