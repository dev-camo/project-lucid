using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SimplifiedBodyManager : ISystem
    {
        private readonly Dictionary<Collider, SimplifiedBody> m_colliderToSimplifiedBody =
            new Dictionary<Collider, SimplifiedBody>();

        public void RegisterSimplifiedBody(SimplifiedBody simplifiedBody, Collider collider)
        {
            m_colliderToSimplifiedBody[collider] = simplifiedBody;
        }

        public void UnregisterSimplifiedBody(SimplifiedBody simplifiedBody, Collider collider)
        {
            m_colliderToSimplifiedBody.Remove(collider);
        }

        public bool TryGetSimplifiedBodyForCollider(Collider collider, out SimplifiedBody simplifiedBody)
        {
            return m_colliderToSimplifiedBody.TryGetValue(collider, out simplifiedBody);
        }

        public SimplifiedBodyManager() { }
    }
}
