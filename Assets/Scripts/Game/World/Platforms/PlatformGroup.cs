using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000776: the shared settings of a group of platform
    // components. The retail player keeps only its data.
    [SelectionBase]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PlatformGroup : MonoBehaviour
    {
        [SerializeField] protected PlatformAssetTypes m_platformAssetType;
        [Tooltip("Local height of the base pieces within this platform group")]
        [SerializeField]
        protected float m_basePieceHeight = -9f;
        [Tooltip("Height of side pieces within this platform group")]
        [SerializeField]
        protected float m_pieceSize = 6f;
        [SerializeField]
        [InspectorReadOnly]
        [Tooltip("List of platform components is auto-populated")]
        protected PlatformComponent[] m_platformComponents;
        [Range(0, 25)] [SerializeField] protected int m_height = 1;
    }
}
