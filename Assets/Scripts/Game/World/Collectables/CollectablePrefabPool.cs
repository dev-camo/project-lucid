using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CollectablePrefabPool : EntityPrefabPool<Collectable>
    {
        protected override void Spawned(in PooledPrefab<Collectable> spawnedObject, UpdateSpawnedObjectCallback callback)
        {
            base.Spawned(in spawnedObject, callback);
            Collectable collectable = spawnedObject.Instance;
            collectable.Action_ManuallyActivate();
            collectable.OnDeactivated += OnDeactivated;
        }

        protected override void OnDeactivated(Collectable collectable)
        {
            base.OnDeactivated(collectable);
            collectable.OnDeactivated -= OnDeactivated;
        }

        public CollectablePrefabPool() { }
    }
}
