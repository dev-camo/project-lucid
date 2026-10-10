using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActiveHomingTarget // 0200029c, five fields and genuine empty ctor 06000e6e.
    {
        public List<IHomingAbility> Abilities;
        public CharacterTargetObjectData Object;
        public float Speed;
        public float RotationalSpeed;
        public Vector3 RestoreVelocity;
        public ActiveHomingTarget() { }
    }
}
