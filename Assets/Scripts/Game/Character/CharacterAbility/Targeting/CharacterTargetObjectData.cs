using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    public struct CharacterTargetObjectData // 0200030e
    {
        public GameObject Target;
        public Vector3 WorldPosition;
        public Quaternion WorldRotation;
        public HomingTargetType Type;
        public TargetObjectMetaData MetaData;
        public bool TryGetMetaDataAsType<T>(out T metaData) where T : TargetObjectMetaData // 060012b6
        {
            // Clear the caller's destination before reading MetaData; aliasing is
            // visible when T is TargetObjectMetaData and the output is that field.
            metaData = null;
            if (MetaData is T value)
            {
                metaData = value;
                return true;
            }
            return false;
        }
    }
}
