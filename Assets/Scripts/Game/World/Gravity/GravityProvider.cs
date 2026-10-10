using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class GravityProvider : MonoBehaviour
    {
        // Original Game.Runtime 06003c08. Use the collection's actual enumerator;
        // callback faults abort registration and dispose the enumerator.
        public void RegisterSources(IReadOnlyCollection<GravitySource> sources)
        {
            foreach (GravitySource source in sources)
                Register(source);
        }

        public abstract Vector3 GetGravity(Vector3 position);
        public abstract void Register(GravitySource source);
        public abstract void Unregister(GravitySource source);
        protected abstract void UnregisterAll();

        // 06003c0d: retain virtual dispatch to the complete provider's unregister path.
        protected virtual void OnDisable()
        {
            UnregisterAll();
        }
    }
}
