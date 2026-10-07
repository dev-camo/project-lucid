using System;

namespace Hardlight
{
    // Original six delegate types. Native runtime wrappers are preserved separately; C# emits their natural API.
    public delegate void MessageCallbackWithCompletion(Action completed);
    public delegate void MessageCallbackWithCompletion<T>(in T obj, Action completed);
    public delegate void MessageCallbackWithCompletion<T1, T2>(in T1 arg1, in T2 arg2, Action completed);
    public delegate void MessageCallbackWithCompletion<T1, T2, T3>(in T1 arg1, in T2 arg2, in T3 arg3, Action completed);
    public delegate void MessageCallbackWithCompletion<T1, T2, T3, T4>(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, Action completed);
    public delegate void MessageCallbackWithCompletion<T1, T2, T3, T4, T5>(in T1 arg1, in T2 arg2, in T3 arg3, in T4 arg4, in T5 arg5, Action completed);
}
