using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [AttributeUsage(AttributeTargets.Class)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FiniteStateMachineJsonCtorArgsAttribute : JSONCtorArgsAttribute
    {
        // Original 0x0600035f. The native optimizer forwards the empty base
        // constructor chain; the original declared immediate base is retained.
        public FiniteStateMachineJsonCtorArgsAttribute() { }
    }
}
