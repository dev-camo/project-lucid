using System;
using System.Collections.Concurrent;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200027e. One registry reference per
    // closed T; it is assigned only by AddAction and has no static initializer.
    public abstract class MainThreadGenericDispatcher<T> : ISystem
    {
        private static SystemRef s_dispatcherRef;
        private readonly ConcurrentQueue<(Action<T> Action, T Parameter)> m_actionQueue = new ConcurrentQueue<(Action<T> Action, T Parameter)>();

        // 0x06000f99; shared 0x17BB590. The queue initializer precedes Object's constructor.
        protected MainThreadGenericDispatcher()
        {
            this.SubscribeToAction(SystemAction.Update, Update);
        }

        // 0x06000f9a; shared 0x17BB680. Both tuple items are read before the
        // null-faulting delegate invoke.
        private void Update(object context)
        {
            while (m_actionQueue.TryDequeue(out (Action<T> Action, T Parameter) entry)) entry.Action(entry.Parameter);
        }

        // 0x06000f9b; shared 0x7B73D0. An existing registration of another
        // dispatcher type reads back as null, so a new TGenericDispatcherType is
        // created, registered under its default name and becomes the reference.
        public static void AddAction<TGenericDispatcherType>(Action<T> action, T parameter)
            where TGenericDispatcherType : MainThreadGenericDispatcher<T>, new()
        {
            // The static is loaded twice, once for the null test and again
            // (null-checked) for GetSafe (0x7B7504, 0x7B7536), so this is not
            // the single-load `?.` form.
            TGenericDispatcherType dispatcher = null;
            if (s_dispatcherRef != null) dispatcher = s_dispatcherRef.GetSafe<TGenericDispatcherType>();
            if (dispatcher == null)
            {
                dispatcher = new TGenericDispatcherType();
                s_dispatcherRef = ProcessManager.RegisterSystem(dispatcher);
            }
            dispatcher.m_actionQueue.Enqueue((action, parameter));
        }
    }

    // Original HLUnityCore.Runtime 0x0200027f; the two-parameter form of the above.
    public abstract class MainThreadGenericDispatcher<T1, T2> : ISystem
    {
        private static SystemRef s_dispatcherRef;
        private readonly ConcurrentQueue<(Action<T1, T2> Action, T1 Parameter1, T2 Parameter2)> m_actionQueue =
            new ConcurrentQueue<(Action<T1, T2> Action, T1 Parameter1, T2 Parameter2)>();

        // 0x06000f9c; shared 0x17BB850.
        protected MainThreadGenericDispatcher()
        {
            this.SubscribeToAction(SystemAction.Update, Update);
        }

        // 0x06000f9d; shared 0x17BB940.
        private void Update(object context)
        {
            while (m_actionQueue.TryDequeue(out (Action<T1, T2> Action, T1 Parameter1, T2 Parameter2) entry))
                entry.Action(entry.Parameter1, entry.Parameter2);
        }

        // 0x06000f9e; shared 0x7B7810.
        public static void AddAction<TGenericDispatcherType>(Action<T1, T2> action, T1 parameter1, T2 parameter2)
            where TGenericDispatcherType : MainThreadGenericDispatcher<T1, T2>, new()
        {
            TGenericDispatcherType dispatcher = null;
            if (s_dispatcherRef != null) dispatcher = s_dispatcherRef.GetSafe<TGenericDispatcherType>();
            if (dispatcher == null)
            {
                dispatcher = new TGenericDispatcherType();
                s_dispatcherRef = ProcessManager.RegisterSystem(dispatcher);
            }
            dispatcher.m_actionQueue.Enqueue((action, parameter1, parameter2));
        }
    }
}
