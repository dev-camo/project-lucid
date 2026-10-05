using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime.dll contract, token 0x06000de5.
    public interface IFastAction
    {
        int GetInvocationListCount();
    }

    // Original Hardlight.FastActionBase`2. Named arm64 generic functions
    // establish mutation queues, duplicate handling and null operator results.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class FastActionBase<TFastAction, TAction> : IFastAction
        where TFastAction : FastActionBase<TFastAction, TAction>, new()
        where TAction : Delegate
    {
        protected readonly List<TAction> m_invocationList;
        protected bool m_invocationActive;
        private readonly List<TAction> m_removedList;
        private readonly List<TAction> m_addedList;

        // 0x06000df0; arm64 0x1347ecc. Only the main list starts at capacity one.
        protected FastActionBase()
        {
            m_invocationList = new List<TAction>(1);
            m_removedList = new List<TAction>();
            m_addedList = new List<TAction>();
        }

        // 0x06000de6; arm64 0x13473ac.
        public static TFastAction operator +(FastActionBase<TFastAction, TAction> fastAction, TAction listener)
        {
            if (listener == null) return (TFastAction)fastAction;
            if (fastAction == null) fastAction = new TFastAction();
            fastAction.AddListener(listener);
            return (TFastAction)fastAction;
        }

        // 0x06000de7; arm64 0x134755c. Added-during-invoke listeners also
        // participate in uniqueness checks; pending removals do not.
        public static TFastAction AddUnique(FastActionBase<TFastAction, TAction> fastAction, TAction listener)
        {
            if (listener == null) return (TFastAction)fastAction;
            if (fastAction == null) fastAction = new TFastAction();
            fastAction.AddListenerUnique(listener);
            return (TFastAction)fastAction;
        }

        // 0x06000de8; arm64 0x1347748. A null listener returns null even when
        // fastAction exists; preserve this observable original result.
        public static TFastAction operator -(FastActionBase<TFastAction, TAction> fastAction, TAction listener)
        {
            if (fastAction == null || listener == null) return null;
            fastAction.RemoveListener(listener);
            return (TFastAction)fastAction;
        }

        // 0x06000de9/0x06000dea; arm64 0x13478b4/0x13478c0.
        public int GetInvocationListCount() => m_invocationList.Count;
        public List<TAction> GetInvocationList() => m_invocationList;

        // 0x06000deb; arm64 0x13478c8. Remove only the first matching entry
        // for each queued removal, in queued order.
        protected static void UpdateListenersRemovedDuringInvoke(TFastAction fastAction)
        {
            if (fastAction.m_removedList.Count == 0) return;
            foreach (TAction listener in fastAction.m_removedList)
                fastAction.m_invocationList.Remove(listener);
            fastAction.m_removedList.Clear();
        }

        // 0x06000dec; arm64 0x1347acc.
        protected static void UpdateListenersAddedDuringInvoke(TFastAction fastAction)
        {
            if (fastAction.m_addedList.Count == 0) return;
            foreach (TAction listener in fastAction.m_addedList)
                fastAction.m_invocationList.Add(listener);
            fastAction.m_addedList.Clear();
        }

        // 0x06000ded; arm64 0x1347d30.
        private void AddListener(TAction listener)
        {
            if (m_invocationActive) m_addedList.Add(listener);
            else m_invocationList.Add(listener);
        }

        // 0x06000dee; arm64 0x1347dbc.
        private void AddListenerUnique(TAction listener)
        {
            if (m_invocationList.Contains(listener)) return;
            if (m_invocationActive && m_addedList.Contains(listener)) return;
            AddListener(listener);
        }

        // 0x06000def; arm64 0x1347e48.
        private void RemoveListener(TAction listener)
        {
            if (m_invocationActive) m_removedList.Add(listener);
            else m_invocationList.Remove(listener);
        }
    }

    // This is the recovered one-argument original type. Other arities and
    // extension methods are separate, unresolved types rather than substitutes.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FastAction<T1> : FastActionBase<FastAction<T1>, Action<T1>>
    {
        // 0x06000df3; arm64 reference instantiation 0x1347fcc. There is no
        // finally: a listener exception leaves invocation active and queues pending.
        // Additions are applied before removals after a successful invocation.
        public static void Invoke(FastAction<T1> fastAction, T1 arg1)
        {
            if (fastAction == null) return;
            fastAction.m_invocationActive = true;
            for (int index = 0; index < fastAction.m_invocationList.Count; index++)
                fastAction.m_invocationList[index](arg1);
            fastAction.m_invocationActive = false;
            UpdateListenersAddedDuringInvoke(fastAction);
            UpdateListenersRemovedDuringInvoke(fastAction);
        }
    }
}
