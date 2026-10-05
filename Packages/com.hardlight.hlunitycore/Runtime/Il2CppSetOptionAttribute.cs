using System;

namespace Unity.IL2CPP.CompilerServices
{
    public enum Option { NullChecks = 1, ArrayBoundsChecks = 2, DivideByZeroChecks = 3 }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property,
        Inherited = false, AllowMultiple = true)]
    public class Il2CppSetOptionAttribute : Attribute
    {
        // Original HLUnityCore.Runtime0x0600002d..31. The constructor calls
        // Attribute then stores the option and the same boxed value reference.
        public Option Option { get; private set; }
        public object Value { get; private set; }
        public Il2CppSetOptionAttribute(Option option, object value) { Option = option; Value = value; }
    }
}
