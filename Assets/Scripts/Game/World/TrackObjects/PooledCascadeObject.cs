using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PooledCascadeObject : MonoBehaviour
    {
        [SerializeField] private SimplifiedBody m_simplifiedBody;
        [SerializeField] private Transform m_launchedTransform;
        [SerializeField] private UnityEvent m_onSpawnedEvent;
        private Vector3 m_baseVelocity;

        // Original 06003e64 preserves the body's authored velocity for each reuse.
        public void Initialise() { m_baseVelocity = m_simplifiedBody.WorldVelocity; }

        // Original 06003e65 updates velocity, conditionally registers gravity sources,
        // resets the launched transform, and finally invokes the optional event.
        public void OnSpawned(Vector3 launchVelocity,
            IReadOnlyCollection<GravitySource> gravitySourcesToAdd = null)
        {
            m_simplifiedBody.WorldVelocity = launchVelocity + m_baseVelocity;
            if (gravitySourcesToAdd != null && m_simplifiedBody.CustomGravity != null)
                m_simplifiedBody.CustomGravity.RegisterSources(gravitySourcesToAdd);
            m_launchedTransform.localPosition = Vector3.zero;
            m_onSpawnedEvent?.Invoke();
        }

        // Original 06003e66 is the natural MonoBehaviour constructor.
    }
}
