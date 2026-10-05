using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [AttributeUsage(AttributeTargets.Field)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InspectorReadOnlyAttribute : PropertyAttribute
    {
        // HLUnityCore.Runtime.dll 0x06000f88: tailcalls Unity PropertyAttribute constructor.
        public InspectorReadOnlyAttribute() { }
    }
}
