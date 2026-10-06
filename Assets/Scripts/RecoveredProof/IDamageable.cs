using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original interface contract; callback is caller supplied and stays exact.
    public interface IDamageable
    {
        DamageAction TryTakeDamage(HazardDefinition hazardDefinition, Action<DamageAction, Collider> processDamageAction);
    }
}
