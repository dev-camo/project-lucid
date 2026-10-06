using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original0200040f has three real abstract contracts, no native bodies.
    public interface IGameplayModifiable
    {
        StackableDataHandle AddModifierOverride<T>(int modifierType, T value);
        void RemoveModifierOverrides(StackableDataHandle stackableDataHandle);
        bool AreModifiersBlocked();
    }
}
