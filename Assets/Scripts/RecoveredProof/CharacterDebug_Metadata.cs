// Original Game.Runtime diagnostic data types (0x02000325..0x02000327).
// Preserves the authored field graph and genuine base-only constructors.
using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks,false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks,false)]
    public class CharacterDebug_Metadata : MonoBehaviour // original02000325
    {
        public Metadata Data;
        public CharacterDebug_Metadata() { } // genuine base-only native060013a7
        [Serializable]
        public struct ValueHolder<T> // original02000326, unconstrainedT, no methods.
        {
            public T Value;
        }
        [Serializable]
        public class Metadata // original02000327, full14 original fields.
        {
            public GameObject GameObject;
            public float Time;
            public Vector3 VelocityWorld;
            public Vector3 VelocityLocal;
            public float Speed;
            public bool BodyIsKinematic;
            public Vector3 BodyVelocity;
            public float BodySpeed;
            public Vector3 Gravity;
            public float GravityMultiplier;
            public ValueHolder<string> TransformInfo;
            public ValueHolder<string> ActorInfo;
            public ValueHolder<string> StateInfo;
            public ValueHolder<string> AnimatorInfo;
            public Metadata() { } // genuine base-only native060013a8; all fields default.
        }
    }
}
