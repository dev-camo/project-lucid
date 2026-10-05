using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000024, all three fields and seven methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(Collider))]
    public class ColliderData : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Option to ignore a collider as tracked for moving.")]
        private bool m_ignoreMovement;

        public bool HasMovement { get; private set; }
        public Vector3 MovementVelocity { get; private set; }
        public bool IgnoreMovement { get { return m_ignoreMovement; } }

        // 0x06000100: retain the caller's velocity even when hasMovement is false.
        // IgnoreMovement is a separate authored tracking choice, not a setter gate.
        public void SetMovement(bool hasMovement, Vector3 movementVelocity)
        {
            HasMovement = hasMovement;
            MovementVelocity = movementVelocity;
        }
    }
}
