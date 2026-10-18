using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000acc: positions at or above a minimum along one
    // world axis are in bounds.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OutOfBoundsPlane : OutOfBoundsTest
    {
        private enum Axis
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        [SerializeField] private Axis m_axis = Axis.Y;
        [SerializeField] private float m_minimumValue = -2f;

        // 06003e56: an axis outside X..Z throws the Vector3 indexer's exception.
        protected override bool PositionIsValid(Vector3 position) => position[(int)m_axis] >= m_minimumValue;
    }
}
