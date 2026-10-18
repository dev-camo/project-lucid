using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a75: draws a gravity source's gizmo for this target.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityRender : MonoBehaviour
    {
        [SerializeField] private GravitySource m_gravitySource;

        // 06003c0f: virtual DrawTargetGizmo slot; an unassigned source throws.
        private void OnDrawGizmos()
        {
            m_gravitySource.DrawTargetGizmo(transform);
        }
    }
}
