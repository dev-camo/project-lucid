using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "StateMachinesGroup", menuName = "HardlightProject/DefinitionData/Groups/StateMachinesGroup")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class StateMachinesGroup : DefinitionDataType<string, FiniteStateMachineScriptableObject>
    {
        // Game.Runtime06001f4c; ARM645315ec tailcalls original Unity Object.get_name.
        // Native Unity null/destroyed-object behavior must be checked in the actual engine.
        protected override string GetElementKey(FiniteStateMachineScriptableObject data) => data.name;
        // Game.Runtime06001f4d; ARM645315f8 returns CLR null, requesting the dictionary default comparer.
        protected override IEqualityComparer<string> GetKeyComparer() => null;
        // Game.Runtime06001f4e; ARM64531600 passes the actual closed base ctor metadata and adds no fields.
    }
}
