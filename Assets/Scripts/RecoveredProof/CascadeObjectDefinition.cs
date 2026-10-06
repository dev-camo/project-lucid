using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CascadeObjectDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CascadeObjectDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class CascadeObjectDefinition : UnityEngine.ScriptableObject
    {
        public HardlightProject.CascadeObjectType Type;

        public Hardlight.PrefabPoolType PrefabPoolType;

        [UnityEngine.Tooltip("Minimum local velocity which is multiplied by a random direction on object spawning.")]
        public UnityEngine.Vector3 MinLaunchVelocity;

        [UnityEngine.Tooltip("Maximum local velocity which is multiplied by a random direction on object spawning.")]
        public UnityEngine.Vector3 MaxLaunchVelocity;

        [UnityEngine.Tooltip("X axis = Spawn number indexed from 0\nY axis = Value between 0 and 1 which is used to slerp between min and max launch velocity.")]
        public UnityEngine.AnimationCurve LaunchVelocityBySpawnedNumberCurve = UnityEngine.AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [UnityEngine.Min(0)]
        [UnityEngine.Tooltip("Objects will disappear after this duration.")]
        public System.Single LifetimeSeconds;

        [UnityEngine.Tooltip("Objects spawned is clamped to this number.")]
        [UnityEngine.Min(0)]
        public System.Int32 MaximumSpawnedObjects;

        [UnityEngine.Tooltip("Maximum range for random direction applied to object on start.")]
        public UnityEngine.Vector3 MaxRandomDirection = UnityEngine.Vector3.one;

        [UnityEngine.Tooltip("Minimum range for random direction applied to object on start.")]
        public UnityEngine.Vector3 MinRandomDirection = -UnityEngine.Vector3.one;

        // Original Game.Runtime 0x06001a8b, ARM 0x5168c4.
        // Native samples x/y/z in this order, normalizes, and falls back to up.
        public Vector3 GetRandomDirection()
        {
            Vector3 direction = new Vector3(
                UnityEngine.Random.Range(MinRandomDirection.x, MaxRandomDirection.x),
                UnityEngine.Random.Range(MinRandomDirection.y, MaxRandomDirection.y),
                UnityEngine.Random.Range(MinRandomDirection.z, MaxRandomDirection.z)).normalized;
            if (direction.sqrMagnitude == 0f) direction = Vector3.up;
            return direction;
        }

        // Original Game.Runtime 0x06001a8c, ARM 0x516a3c.
        // Genuine supplied-player RET; no inferred Editor validation.
        private void OnValidate() { }

        // Original Game.Runtime 0x06001a8d, ARM 0x516a40.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
