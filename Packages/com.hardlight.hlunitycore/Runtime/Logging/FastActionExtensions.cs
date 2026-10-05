using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class FastActionExtensions
    {
        // 0x06000df9; ARM64 0x1b18038 directly branches to original Invoke.
        public static void Invoke(this FastAction fastAction) => FastAction.Invoke(fastAction);

        // 0x06000dfa; ARM64 int instance 0x90ab60.
        public static void Invoke<T>(this FastAction<T> fastAction, T arg) => FastAction<T>.Invoke(fastAction, arg);

        // 0x06000dfb; ARM64 reference/int instance 0x90ae24.
        public static void Invoke<T1, T2>(this FastAction<T1, T2> fastAction, T1 arg1, T2 arg2) =>
            FastAction<T1, T2>.Invoke(fastAction, arg1, arg2);

        // 0x06000dfc; ARM64 reference/reference/int instance 0x90b078.
        public static void Invoke<T1, T2, T3>(this FastAction<T1, T2, T3> fastAction, T1 arg1, T2 arg2, T3 arg3) =>
            FastAction<T1, T2, T3>.Invoke(fastAction, arg1, arg2, arg3);

        // 0x06000dfd; ARM64 reference instance 0x90ab14. All duplicate/null
        // semantics remain in the maintained original base method 0x06000de7.
        public static TFastAction AddUnique<TFastAction, TAction>(this FastActionBase<TFastAction, TAction> fastAction, TAction listener)
            where TFastAction : FastActionBase<TFastAction, TAction>, new()
            where TAction : Delegate => FastActionBase<TFastAction, TAction>.AddUnique(fastAction, listener);
    }
}
