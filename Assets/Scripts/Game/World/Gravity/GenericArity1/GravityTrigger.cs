using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a7c. The authored description and the typed
    // collider are published here; the shapes derive their collider layout.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public abstract class GravityTrigger<T> : GravityTrigger where T : Collider
    {
        [SerializeField] private GravityDescription m_description;

        protected T m_collider { get; private set; }

        // 06003c4c (shared 0x13913f0): publish the description first, then the
        // collider. The collider is forced to a trigger on every recalculation.
        protected override void CalculateCachedValues()
        {
            SetDescription(m_description);
            m_collider = GetComponent<T>();
            m_collider.isTrigger = true;
        }

        protected GravityTrigger() { }
    }
}
