using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FastAction : FastActionBase<FastAction, Action>
    {
        // HLUnityCore.Runtime 0x06000df1; original named ARM64 0x1b185fc.
        // Direct mutations use the live list count. Exceptions retain the active
        // flag and pending queues; successful calls apply additions before removals.
        public static void Invoke(FastAction fastAction)
        {
            if (fastAction == null) return;
            fastAction.m_invocationActive = true;
            for (int index = 0; index < fastAction.m_invocationList.Count; index++)
                fastAction.m_invocationList[index]();
            fastAction.m_invocationActive = false;
            UpdateListenersAddedDuringInvoke(fastAction);
            UpdateListenersRemovedDuringInvoke(fastAction);
        }

        // 0x06000df2; original named ARM64 0x1b18704 tailcalls the real base ctor.
        public FastAction() { }
    }
}
