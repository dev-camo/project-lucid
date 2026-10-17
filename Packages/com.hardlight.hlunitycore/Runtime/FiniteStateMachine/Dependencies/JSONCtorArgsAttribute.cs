using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [AttributeUsage(AttributeTargets.Class)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class JSONCtorArgsAttribute : Attribute
    {
        // HLUnityCore.Runtime.dll:Hardlight.JSONCtorArgsAttribute:0x0600093a;
        // arm64 0x1af5eb4 delegates only to System.Attribute's constructor.
        public JSONCtorArgsAttribute() { }
    }
}
