using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000acb: positions inside the world-space box are in bounds.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OutOfBoundsBox : OutOfBoundsTest
    {
        [SerializeField] private Bounds m_boundingBox;

        protected override bool PositionIsValid(Vector3 position) => m_boundingBox.Contains(position);
    }
}
