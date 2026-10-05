using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FastAction<T1, T2> : FastActionBase<FastAction<T1, T2>, Action<T1, T2>>
    {
        // HLUnityCore.Runtime 0x06000df5; original ARM64 reference/int instance
        // 0x1349458. List count stays live; no finally drains failed dispatch.
        public static void Invoke(FastAction<T1, T2> fastAction, T1 arg1, T2 arg2)
        {
            if (fastAction == null) return;
            fastAction.m_invocationActive = true;
            for (int index = 0; index < fastAction.m_invocationList.Count; index++)
                fastAction.m_invocationList[index](arg1, arg2);
            fastAction.m_invocationActive = false;
            UpdateListenersAddedDuringInvoke(fastAction);
            UpdateListenersRemovedDuringInvoke(fastAction);
        }

        // 0x06000df6; ARM64 0x1349578 tailcalls the genuine generic base ctor.
        public FastAction() { }
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FastAction<T1, T2, T3> : FastActionBase<FastAction<T1, T2, T3>, Action<T1, T2, T3>>
    {
        // 0x06000df7; ARM64 reference/reference/int instance 0x134a5b8.
        // Successful dispatch applies queued additions before queued removals.
        public static void Invoke(FastAction<T1, T2, T3> fastAction, T1 arg1, T2 arg2, T3 arg3)
        {
            if (fastAction == null) return;
            fastAction.m_invocationActive = true;
            for (int index = 0; index < fastAction.m_invocationList.Count; index++)
                fastAction.m_invocationList[index](arg1, arg2, arg3);
            fastAction.m_invocationActive = false;
            UpdateListenersAddedDuringInvoke(fastAction);
            UpdateListenersRemovedDuringInvoke(fastAction);
        }

        // 0x06000df8; ARM64 0x134a6e8 tailcalls the genuine generic base ctor.
        public FastAction() { }
    }
}
