using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000823: pans the local position on X and Y at the
    // given speeds, wrapping each axis at the pan amount in the speed's direction.
    // The local Z is set to 0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformPanner : TimeScaledComponent_SDT
    {
        [Space(10)]
        [SerializeField]
        [Header("Moves an object by a fixed amount over a fixed length of time (also works with UI).", order = 0)]
        private Vector2 m_amountToPan = Vector2.one;
        [SerializeField]
        private Vector2 m_speedToPan = Vector2.one;
        private float m_desiredX;
        private float m_desiredY;
        private float m_trackedX;
        private float m_trackedY;

        // A non-positive (or NaN) speed pans towards the negated amount. An axis whose
        // amount is within float.Epsilon of zero is never wrapped.
        protected override void InternalUpdate(float deltaTime)
        {
            m_desiredX = m_speedToPan.x > 0f ? m_amountToPan.x : -m_amountToPan.x;
            m_desiredY = m_speedToPan.y > 0f ? m_amountToPan.y : -m_amountToPan.y;
            m_trackedX += m_speedToPan.x * deltaTime;
            if (Math.Abs(m_desiredX) > float.Epsilon)
                m_trackedX %= m_desiredX;
            m_trackedY += m_speedToPan.y * deltaTime;
            if (Math.Abs(m_desiredY) > float.Epsilon)
                m_trackedY %= m_desiredY;
            Vector3 position = new Vector3(m_trackedX, m_trackedY, 0f);
            transform.localPosition = position;
        }
    }
}
