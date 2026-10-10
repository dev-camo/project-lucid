using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    public interface IEntityPrefabPool : IPrefabPool
    {
        void SpawnInstance(Transform transform, float scaleMod = 1f, UpdateSpawnedObjectCallback callback = null);
    }
}
