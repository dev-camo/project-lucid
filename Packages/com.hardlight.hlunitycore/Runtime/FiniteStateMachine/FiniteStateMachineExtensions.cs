using System.Collections.Generic;

namespace Hardlight
{
    public static class FiniteStateMachineExtensions
    {
        // 0x06000391; arm64 0x1abefac, original name registration order.
        private static readonly int s_activeStateId = GraphNameLookup.ConvertNameToId("activeState");
        private static readonly int s_stateHistoryId = GraphNameLookup.ConvertNameToId("stateHistory");
        private static readonly int s_stateHistoryMaxCountId = GraphNameLookup.ConvertNameToId("stateHistoryMaxCount");

        // 0x0600038a; arm64 0x1abcccc: store before recording history.
        public static void SetActiveState(this FiniteStateMachine fsm, IGraphStorage storage, FSMStateChangeAction action)
        {
            storage.SetValue<IFSMState>(new GraphStorageKey(s_activeStateId, 0, fsm.FSMId), action.ToState);
            AddStateToHistory(fsm, storage, action);
        }

        // 0x0600038b; arm64 0x1abd4c4. The retail binary compares the static
        // *identifier* at field offset 8 with 1, not the stored maximum count.
        // Its normal signed CRC is negative, so it skips the removal record.
        public static void RemoveActiveState(this FiniteStateMachine fsm, IGraphStorage storage, FSMStateChangeAction action)
        {
            storage.RemoveValue<IFSMState>(new GraphStorageKey(s_activeStateId, 0, fsm.FSMId));
            if (s_stateHistoryMaxCountId >= 1)
            {
                var removal = FSMStateChangeAction.Create(FSMActionReason.RemoveActiveState, parentAction: action);
                AddStateToHistory(fsm, storage, removal);
                removal.Destroy();
            }
        }

        // 0x0600038c; arm64 0x1abc840: a missing active entry stores null.
        public static IFSMState GetActiveState(this FiniteStateMachine fsm, IGraphStorage storage) =>
            storage.GetValue<IFSMState>(new GraphStorageKey(s_activeStateId, 0, fsm.FSMId), null, true);

        // 0x0600038d; arm64 0x1abebd4.
        public static IReadOnlyCollection<FSMStateChangeAction> GetStateHistory(this FiniteStateMachine fsm, IGraphStorage storage)
        {
            Dictionary<int, Queue<FSMStateChangeAction>> history = MyStateHistory(storage);
            if (history == null) return null;
            return history.TryGetValue(fsm.FSMId, out Queue<FSMStateChangeAction> queue) ? queue : null;
        }

        // 0x0600038e; arm64 0x1abe9c8. Evicts exactly one entry, even if
        // the maximum was changed to less than the current queue count.
        private static void AddStateToHistory(FiniteStateMachine fsm, IGraphStorage storage, FSMStateChangeAction action)
        {
            int maxCount = StateHistoryMaxCount(storage);
            if (maxCount < 1) return;
            Dictionary<int, Queue<FSMStateChangeAction>> history = MyStateHistory(storage);
            if (!history.TryGetValue(fsm.FSMId, out Queue<FSMStateChangeAction> queue))
            {
                queue = new Queue<FSMStateChangeAction>(maxCount);
                history.Add(fsm.FSMId, queue);
            }
            if (unchecked(queue.Count + 1) > maxCount) queue.Dequeue().Destroy(true);
            queue.Enqueue(action.Clone());
        }

        // 0x0600038f; arm64 0x1abedc0.
        private static int StateHistoryMaxCount(IGraphStorage storage) => storage.GetValue<int>(s_stateHistoryMaxCountId, 0, false);
        // 0x06000390; arm64 0x1abec90.
        private static Dictionary<int, Queue<FSMStateChangeAction>> MyStateHistory(IGraphStorage storage) =>
            storage.GetValue<Dictionary<int, Queue<FSMStateChangeAction>>>(s_stateHistoryId, null, false);
    }
}
