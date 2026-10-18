using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a7d: the global gravity turned by this transform's
    // rotation when it is enabled, for every position. Sources are never tracked.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RotationGravity : GravityProvider
    {
        private Vector3 m_gravity;

        private void OnEnable()
        {
            m_gravity = transform.rotation * Physics.gravity;
        }

        public override Vector3 GetGravity(Vector3 position)
        {
            return m_gravity;
        }

        public override void Register(GravitySource source)
        {
        }

        public override void Unregister(GravitySource source)
        {
        }

        protected override void UnregisterAll()
        {
        }
    }
}
