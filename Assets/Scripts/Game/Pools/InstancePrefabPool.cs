using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InstancePrefabPool : MonoBehaviour
    {
        // Original 06002a25..27 are genuinely abstract, not missing implementations.
        public abstract void Instantiate(BoundsOctree<InstancePrefabProxy> octree, ref float cullDistance);
        public abstract void UpdateProxies();
        public abstract void GetInfo(StringBuilder stringInfoBuilder);
        // Original 06002a28 is the natural protected abstract-class constructor.
    }
}
